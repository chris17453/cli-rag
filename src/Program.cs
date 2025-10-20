using System.CommandLine;
using CliRag.Config;
using CliRag.Ingestion;
using CliRag.Embeddings;
using CliRag.VectorStore;
using CliRag.LLM;
using CliRag.Agent;
using Spectre.Console;

namespace CliRag;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("CLI-RAG: Local Agentic RAG for document Q&A");

        // Ingest command
        var ingestCommand = new Command("ingest", "Ingest a URL into the knowledge base");
        var urlArgument = new Argument<string>("url", "The URL to ingest");
        ingestCommand.AddArgument(urlArgument);
        ingestCommand.SetHandler(IngestUrlAsync, urlArgument);
        rootCommand.AddCommand(ingestCommand);

        // Crawl command
        var crawlCommand = new Command("crawl", "Crawl and ingest an entire website (stays within same domain)");
        var crawlUrlArgument = new Argument<string>("url", "The starting URL to crawl");
        var maxPagesOption = new Option<int>("--max-pages", () => 50, "Maximum number of pages to crawl");
        maxPagesOption.AddAlias("-m");
        crawlCommand.AddArgument(crawlUrlArgument);
        crawlCommand.AddOption(maxPagesOption);
        crawlCommand.SetHandler(CrawlWebsiteAsync, crawlUrlArgument, maxPagesOption);
        rootCommand.AddCommand(crawlCommand);

        // Query command
        var queryCommand = new Command("query", "Interactive query mode");
        var agenticOption = new Option<bool>("--agentic", () => false, "Use true agentic RAG mode with multi-hop retrieval and self-reflection");
        agenticOption.AddAlias("-a");
        var showReasoningOption = new Option<bool>("--show-reasoning", () => false, "Show reasoning steps (agentic mode only)");
        showReasoningOption.AddAlias("-r");
        queryCommand.AddOption(agenticOption);
        queryCommand.AddOption(showReasoningOption);
        queryCommand.SetHandler(QueryModeAsync, agenticOption, showReasoningOption);
        rootCommand.AddCommand(queryCommand);

        // List command
        var listCommand = new Command("list", "List all ingested documents");
        listCommand.SetHandler(ListDocumentsAsync);
        rootCommand.AddCommand(listCommand);

        // List crawled command
        var listCrawledCommand = new Command("list-crawled", "List all crawled websites");
        listCrawledCommand.SetHandler(ListCrawledUrlsAsync);
        rootCommand.AddCommand(listCrawledCommand);

        // Clear command
        var clearCommand = new Command("clear", "Clear all documents from the database");
        var forceOption = new Option<bool>("--force", () => false, "Skip confirmation prompt");
        forceOption.AddAlias("-f");
        clearCommand.AddOption(forceOption);
        clearCommand.SetHandler(ClearDatabaseAsync, forceOption);
        rootCommand.AddCommand(clearCommand);

        return await rootCommand.InvokeAsync(args);
    }

    static async Task IngestUrlAsync(string url)
    {
        AnsiConsole.Write(new Rule("[cyan]Ingesting URL[/]").RuleStyle("dim"));

        var config = AppConfig.Load();
        using var embeddings = new LocalEmbeddings();
        using var vectorDb = new VectorDb(config, embeddings);

        // Check if already ingested
        if (await vectorDb.IsUrlIngestedAsync(url))
        {
            AnsiConsole.MarkupLine($"[yellow]URL already ingested:[/] {url}");
            AnsiConsole.MarkupLine($"[dim]Use 'list' to see all ingested URLs[/]");
            return;
        }

        // Fetch URL
        var fetcher = new UrlFetcher();
        var document = await fetcher.FetchAsync(url);

        if (document == null)
        {
            AnsiConsole.MarkupLine("[red]Failed to fetch URL[/]");
            return;
        }

        // Chunk document
        var processor = new DocumentProcessor(config);
        var chunks = processor.ChunkDocument(document);
        AnsiConsole.MarkupLine($"[cyan]Created {chunks.Count} chunks[/]");

        // Add to vector store
        await vectorDb.AddChunksAsync(chunks);

        // Mark as ingested
        await vectorDb.MarkUrlIngestedAsync(url, document.Title, chunks.Count);

        AnsiConsole.Write(new Rule("[green]Ingestion Complete[/]").RuleStyle("dim"));
    }

    static async Task CrawlWebsiteAsync(string url, int maxPages)
    {
        AnsiConsole.Write(new Rule("[cyan]Crawling Website[/]").RuleStyle("dim"));
        AnsiConsole.WriteLine();

        var config = AppConfig.Load();
        using var embeddings = new LocalEmbeddings();
        using var vectorDb = new VectorDb(config, embeddings);

        // Crawl website
        var crawler = new WebCrawler(url, maxPages, vectorDb);
        var documents = await crawler.CrawlAsync(url);

        if (documents.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No documents found[/]");
            return;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[cyan]Processing {documents.Count} documents...[/]");

        // Process and store documents
        var processor = new DocumentProcessor(config);
        var totalChunks = 0;

        foreach (var document in documents)
        {
            var chunks = processor.ChunkDocument(document);
            await vectorDb.AddChunksAsync(chunks);
            totalChunks += chunks.Count;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[green]Crawled {documents.Count} pages and created {totalChunks} chunks[/]");
        AnsiConsole.Write(new Rule("[green]Crawl Complete[/]").RuleStyle("dim"));
    }

    static async Task QueryModeAsync(bool useAgentic, bool showReasoning)
    {
        var modeText = useAgentic ? "True Agentic RAG Mode" : "Standard RAG Mode";
        AnsiConsole.Write(new Rule($"[cyan]Interactive Query Mode - {modeText}[/]").RuleStyle("dim"));
        AnsiConsole.MarkupLine("[dim]Type 'exit' or 'quit' to leave[/]");
        if (useAgentic && showReasoning)
        {
            AnsiConsole.MarkupLine("[dim]Reasoning steps will be displayed[/]");
        }
        AnsiConsole.WriteLine();

        var config = AppConfig.Load();
        using var embeddings = new LocalEmbeddings();
        using var vectorDb = new VectorDb(config, embeddings);
        using var llm = new LocalLLM(config);

        if (!llm.IsAvailable)
        {
            AnsiConsole.MarkupLine("[red]LLM not available. Please configure a model.[/]");
            return;
        }

        // Choose RAG implementation based on flag
        object agent;
        if (useAgentic)
        {
            agent = new TrueAgenticRAG(llm, vectorDb, config);
        }
        else
        {
            agent = new AgenticRAG(llm, vectorDb, config);
        }

        while (true)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Markup("[cyan]>[/] ");

            // Use Console.ReadLine for better compatibility
            var query = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(query))
                continue;

            if (query.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
                query.Equals("quit", StringComparison.OrdinalIgnoreCase))
            {
                AnsiConsole.MarkupLine("[dim]Goodbye![/]");
                break;
            }

            AnsiConsole.WriteLine();

            AgenticResponse response;
            if (useAgentic)
            {
                var trueAgent = (TrueAgenticRAG)agent;
                response = await trueAgent.QueryAsync(query, showReasoning);
            }
            else
            {
                var standardAgent = (AgenticRAG)agent;
                response = await standardAgent.QueryAsync(query);
            }

            // Display answer (escape markup and clean special tokens)
            var cleanAnswer = response.Answer
                .Replace("</s>", "")
                .Replace("<|eot_id|>", "")
                .Replace("�", "")
                .Trim();
            var escapedAnswer = cleanAnswer.Replace("[", "[[").Replace("]", "]]");
            var panel = new Panel(escapedAnswer)
            {
                Header = new PanelHeader("[green]Answer[/]"),
                Border = BoxBorder.Rounded,
                Padding = new Padding(1, 1),
                Expand = true
            };
            AnsiConsole.Write(panel);

            // Display sources
            if (response.Sources.Count > 0)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[dim]Sources:[/]");

                var table = new Table()
                    .Border(TableBorder.Rounded)
                    .BorderColor(Color.Grey)
                    .AddColumn("#")
                    .AddColumn("Title")
                    .AddColumn(new TableColumn("Score").RightAligned());

                foreach (var source in response.Sources.Take(5))
                {
                    table.AddRow(
                        $"[cyan]{source.Index}[/]",
                        $"[dim]{source.Title}[/]",
                        $"[yellow]{source.Score:F2}[/]"
                    );
                }

                AnsiConsole.Write(table);
            }

            // Display reasoning steps if in agentic mode (always show them, not just when --show-reasoning)
            if (useAgentic && response.ReasoningSteps.Count > 0)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.Write(new Rule("[cyan]Reasoning Process[/]").RuleStyle("dim"));

                foreach (var step in response.ReasoningSteps)
                {
                    var escapedStep = step.Replace("[", "[[").Replace("]", "]]");
                    AnsiConsole.MarkupLine($"[dim]→ {escapedStep}[/]");
                }
            }
        }
    }

    static async Task ListDocumentsAsync()
    {
        var config = AppConfig.Load();
        using var embeddings = new LocalEmbeddings();
        using var vectorDb = new VectorDb(config, embeddings);

        var ingestedUrls = await vectorDb.ListIngestedUrlsAsync();
        var crawledUrls = await vectorDb.ListCrawledUrlsAsync();

        if (ingestedUrls.Count == 0 && crawledUrls.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No URLs in database[/]");
            return;
        }

        AnsiConsole.Write(new Rule("[cyan]All URLs[/]").RuleStyle("dim"));
        AnsiConsole.WriteLine();

        // Show ingested URLs
        if (ingestedUrls.Count > 0)
        {
            AnsiConsole.MarkupLine("[bold cyan]Ingested URLs:[/]");
            var ingestedTable = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Grey)
                .AddColumn("URL")
                .AddColumn("Title")
                .AddColumn("Chunks")
                .AddColumn(new TableColumn("Ingested At").RightAligned());

            foreach (var url in ingestedUrls)
            {
                ingestedTable.AddRow(
                    $"[cyan]{url.Url}[/]",
                    $"[dim]{url.Title}[/]",
                    $"[yellow]{url.Chunk_Count}[/]",
                    $"[dim]{url.Ingested_At}[/]"
                );
            }

            AnsiConsole.Write(ingestedTable);
            AnsiConsole.WriteLine();
        }

        // Show crawled URLs
        if (crawledUrls.Count > 0)
        {
            AnsiConsole.MarkupLine("[bold cyan]Crawled Websites:[/]");
            var crawledTable = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Grey)
                .AddColumn("Base URL")
                .AddColumn("Pages")
                .AddColumn(new TableColumn("Crawled At").RightAligned());

            foreach (var url in crawledUrls)
            {
                crawledTable.AddRow(
                    $"[cyan]{url.Base_Url}[/]",
                    $"[yellow]{url.Page_Count}[/]",
                    $"[dim]{url.Crawled_At}[/]"
                );
            }

            AnsiConsole.Write(crawledTable);
        }
    }

    static async Task ListCrawledUrlsAsync()
    {
        var config = AppConfig.Load();
        using var embeddings = new LocalEmbeddings();
        using var vectorDb = new VectorDb(config, embeddings);

        var urls = await vectorDb.ListCrawledUrlsAsync();

        if (urls.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No crawled websites yet[/]");
            return;
        }

        AnsiConsole.Write(new Rule("[cyan]Crawled Websites[/]").RuleStyle("dim"));
        AnsiConsole.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn("URL")
            .AddColumn("Pages")
            .AddColumn(new TableColumn("Crawled At").RightAligned());

        foreach (var url in urls)
        {
            table.AddRow(
                $"[cyan]{url.Base_Url}[/]",
                $"[yellow]{url.Page_Count}[/]",
                $"[dim]{url.Crawled_At}[/]"
            );
        }

        AnsiConsole.Write(table);
    }

    static async Task ClearDatabaseAsync(bool force)
    {
        if (!force && !AnsiConsole.Confirm("[yellow]Are you sure you want to clear all documents?[/]"))
        {
            AnsiConsole.MarkupLine("[dim]Cancelled[/]");
            return;
        }

        var config = AppConfig.Load();
        using var embeddings = new LocalEmbeddings();
        using var vectorDb = new VectorDb(config, embeddings);

        await vectorDb.ClearAllAsync();
    }
}
