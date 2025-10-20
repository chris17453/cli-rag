using CliRag.Config;
using CliRag.LLM;
using CliRag.VectorStore;
using Spectre.Console;

namespace CliRag.Agent;

/// <summary>
/// Agentic RAG system that performs multi-step reasoning:
/// 1. Query decomposition
/// 2. Retrieval with relevance filtering
/// 3. Multi-hop reasoning
/// 4. Answer synthesis with citations
/// </summary>
public class AgenticRAG
{
    private readonly LocalLLM _llm;
    private readonly VectorDb _vectorDb;
    private readonly AppConfig _config;

    public AgenticRAG(LocalLLM llm, VectorDb vectorDb, AppConfig config)
    {
        _llm = llm;
        _vectorDb = vectorDb;
        _config = config;
    }

    public async Task<AgenticResponse> QueryAsync(string userQuery)
    {
        if (!_llm.IsAvailable)
        {
            return new AgenticResponse
            {
                Answer = "Error: LLM is not available. Please configure a model.",
                Sources = new List<SourceCitation>(),
                ReasoningSteps = new List<string> { "LLM not available" }
            };
        }

        var reasoningSteps = new List<string>();
        var sources = new List<SourceCitation>();

        try
        {
            // Step 1: Analyze and decompose query
            AnsiConsole.MarkupLine("[cyan]Step 1: Analyzing query...[/]");
            reasoningSteps.Add("Query analysis");

            var queries = await DecomposeQueryAsync(userQuery);
            reasoningSteps.Add($"Decomposed into {queries.Count} sub-queries");

            // Step 2: Retrieve relevant context for each sub-query
            AnsiConsole.MarkupLine("[cyan]Step 2: Retrieving relevant context...[/]");
            var allResults = new List<SearchResult>();

            foreach (var query in queries)
            {
                var results = await _vectorDb.SearchAsync(query, _config.TopK);
                allResults.AddRange(results);
                reasoningSteps.Add($"Found {results.Count} results for: {query}");
            }

            // Deduplicate and rank
            var uniqueResults = allResults
                .GroupBy(r => r.Text)
                .Select(g => g.OrderByDescending(r => r.Score).First())
                .OrderByDescending(r => r.Score)
                .Take(_config.TopK * 2)
                .ToList();

            if (uniqueResults.Count == 0)
            {
                return new AgenticResponse
                {
                    Answer = "I don't have enough information to answer this question. Please ingest relevant documents first.",
                    Sources = new List<SourceCitation>(),
                    ReasoningSteps = reasoningSteps
                };
            }

            reasoningSteps.Add($"Selected {uniqueResults.Count} unique chunks");

            // Prepare sources
            sources = uniqueResults.Select((r, i) => new SourceCitation
            {
                Index = i + 1,
                Url = r.Url,
                Title = r.Title,
                Text = r.Text,
                Score = r.Score
            }).ToList();

            // Step 3: Generate answer with citations
            AnsiConsole.MarkupLine("[cyan]Step 3: Generating answer...[/]");
            var answer = await GenerateAnswerAsync(userQuery, uniqueResults);
            reasoningSteps.Add("Generated final answer");

            return new AgenticResponse
            {
                Answer = answer,
                Sources = sources,
                ReasoningSteps = reasoningSteps
            };
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error during RAG: {ex.Message}[/]");
            return new AgenticResponse
            {
                Answer = $"Error processing query: {ex.Message}",
                Sources = sources,
                ReasoningSteps = reasoningSteps
            };
        }
    }

    private async Task<List<string>> DecomposeQueryAsync(string query)
    {
        // For simplicity, we'll use basic decomposition
        // In a full agentic system, the LLM would decompose complex queries
        var queries = new List<string> { query };

        // Add expanded queries for better retrieval
        if (query.Contains("how") || query.Contains("what") || query.Contains("why"))
        {
            // Extract key terms
            var words = query.ToLower()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 3 && !IsStopWord(w))
                .ToList();

            if (words.Count > 0)
            {
                queries.Add(string.Join(" ", words));
            }
        }

        return await Task.FromResult(queries);
    }

    private async Task<string> GenerateAnswerAsync(string query, List<SearchResult> context)
    {
        // Limit context to avoid overwhelming the model
        var topContext = context.Take(3).ToList();
        var contextText = string.Join("\n\n", topContext.Select(r => r.Text));

        // Don't manually add BOS token - LLamaSharp adds it automatically
        var prompt = $@"<|start_header_id|>system<|end_header_id|>

You are a helpful assistant. Read the context and answer the question. Start your response with 'ANSWER:' followed by your answer.<|eot_id|><|start_header_id|>user<|end_header_id|>

Context:
{contextText}

Question: {query}<|eot_id|><|start_header_id|>assistant<|end_header_id|>

ANSWER:";

        var response = await _llm.GenerateAsync(prompt);

        // Extract answer after "ANSWER:" marker if present
        if (response.Contains("ANSWER:", StringComparison.OrdinalIgnoreCase))
        {
            var answerStart = response.IndexOf("ANSWER:", StringComparison.OrdinalIgnoreCase) + 7;
            response = response.Substring(answerStart).Trim();
        }

        return response;
    }

    private static bool IsStopWord(string word)
    {
        var stopWords = new HashSet<string>
        {
            "the", "is", "at", "which", "on", "a", "an", "and", "or", "but",
            "in", "with", "to", "for", "of", "as", "by", "from", "this", "that"
        };
        return stopWords.Contains(word.ToLower());
    }
}

public class AgenticResponse
{
    public required string Answer { get; set; }
    public required List<SourceCitation> Sources { get; set; }
    public required List<string> ReasoningSteps { get; set; }
}

public class SourceCitation
{
    public int Index { get; set; }
    public required string Url { get; set; }
    public required string Title { get; set; }
    public required string Text { get; set; }
    public float Score { get; set; }
}
