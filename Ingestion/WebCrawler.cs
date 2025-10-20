using HtmlAgilityPack;
using Spectre.Console;
using System.Text.RegularExpressions;
using CliRag.VectorStore;

namespace CliRag.Ingestion;

public partial class WebCrawler
{
    private readonly UrlFetcher _fetcher;
    private readonly HashSet<string> _visited;
    private readonly HashSet<string> _queued;
    private readonly Uri _baseUri;
    private readonly int _maxPages;
    private readonly VectorDb? _vectorDb;

    public WebCrawler(string baseUrl, int maxPages = 50, VectorDb? vectorDb = null)
    {
        _fetcher = new UrlFetcher();
        _visited = new HashSet<string>();
        _queued = new HashSet<string>();
        _baseUri = new Uri(baseUrl);
        _maxPages = maxPages;
        _vectorDb = vectorDb;
    }

    public async Task<List<DocumentContent>> CrawlAsync(string startUrl)
    {
        // Check if this URL has already been crawled
        if (_vectorDb != null)
        {
            var baseUrlString = $"{_baseUri.Scheme}://{_baseUri.Host}";
            if (await _vectorDb.IsUrlCrawledAsync(baseUrlString))
            {
                AnsiConsole.MarkupLine($"[yellow]URL already crawled:[/] {baseUrlString}");
                AnsiConsole.MarkupLine($"[dim]Use 'list-crawled' to see all crawled sites[/]");
                return new List<DocumentContent>();
            }
        }

        var documents = new List<DocumentContent>();
        var queue = new Queue<string>();

        queue.Enqueue(NormalizeUrl(startUrl));
        _queued.Add(NormalizeUrl(startUrl));

        AnsiConsole.MarkupLine($"[cyan]Starting crawl of:[/] {_baseUri.Host}");
        AnsiConsole.MarkupLine($"[dim]Max pages: {_maxPages}[/]");
        AnsiConsole.WriteLine();

        await AnsiConsole.Progress()
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[cyan]Crawling pages...[/]", maxValue: _maxPages);

                while (queue.Count > 0 && _visited.Count < _maxPages)
                {
                    var url = queue.Dequeue();

                    if (_visited.Contains(url))
                        continue;

                    _visited.Add(url);
                    task.Description = $"[cyan]Crawling:[/] {GetShortUrl(url)}";

                    var document = await _fetcher.FetchAsync(url);

                    if (document != null)
                    {
                        documents.Add(document);

                        // Extract and queue new links
                        var links = await ExtractLinksAsync(url);
                        foreach (var link in links)
                        {
                            if (!_visited.Contains(link) && !_queued.Contains(link))
                            {
                                queue.Enqueue(link);
                                _queued.Add(link);
                            }
                        }
                    }

                    task.Increment(1);
                    task.MaxValue = Math.Max(task.MaxValue, _visited.Count + queue.Count);

                    // Small delay to be respectful
                    await Task.Delay(500);
                }

                task.StopTask();
            });

        AnsiConsole.MarkupLine($"[green]Crawled {documents.Count} pages from {_baseUri.Host}[/]");

        // Mark URL as crawled
        if (_vectorDb != null && documents.Count > 0)
        {
            var baseUrlString = $"{_baseUri.Scheme}://{_baseUri.Host}";
            await _vectorDb.MarkUrlCrawledAsync(baseUrlString, documents.Count);
        }

        return documents;
    }

    private async Task<List<string>> ExtractLinksAsync(string url)
    {
        var links = new List<string>();

        try
        {
            var response = await _fetcher._httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync();
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var linkNodes = doc.DocumentNode.SelectNodes("//a[@href]");
            if (linkNodes == null)
                return links;

            foreach (var node in linkNodes)
            {
                var href = node.GetAttributeValue("href", "");
                if (string.IsNullOrWhiteSpace(href))
                    continue;

                var absoluteUrl = MakeAbsoluteUrl(url, href);
                if (absoluteUrl != null && IsSameDomain(absoluteUrl))
                {
                    links.Add(NormalizeUrl(absoluteUrl));
                }
            }
        }
        catch
        {
            // Ignore errors during link extraction
        }

        return links.Distinct().ToList();
    }

    private string? MakeAbsoluteUrl(string baseUrl, string relativeUrl)
    {
        try
        {
            // Remove fragments and query params for deduplication
            relativeUrl = FragmentRegex().Replace(relativeUrl, "");

            var uri = new Uri(new Uri(baseUrl), relativeUrl);
            return uri.ToString();
        }
        catch
        {
            return null;
        }
    }

    private bool IsSameDomain(string url)
    {
        try
        {
            var uri = new Uri(url);
            return uri.Host.Equals(_baseUri.Host, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private string NormalizeUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            // Remove fragment and trailing slash
            var normalized = $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath.TrimEnd('/')}";
            if (!string.IsNullOrEmpty(uri.Query))
                normalized += uri.Query;
            return normalized;
        }
        catch
        {
            return url;
        }
    }

    private string GetShortUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var path = uri.AbsolutePath;
            if (path.Length > 50)
                path = "..." + path.Substring(path.Length - 47);
            return path;
        }
        catch
        {
            return url;
        }
    }

    [GeneratedRegex(@"#.*$")]
    private static partial Regex FragmentRegex();
}
