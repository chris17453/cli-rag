using HtmlAgilityPack;
using Spectre.Console;

namespace CliRag.Ingestion;

public class UrlFetcher
{
    public readonly HttpClient _httpClient;

    public UrlFetcher()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "CliRag/1.0");
    }

    public async Task<DocumentContent?> FetchAsync(string url)
    {
        try
        {
            AnsiConsole.MarkupLine($"[cyan]Fetching URL:[/] {url}");

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync();
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Remove unwanted elements
            var nodesToRemove = doc.DocumentNode.SelectNodes("//script | //style | //nav | //footer | //header");
            if (nodesToRemove != null)
            {
                foreach (var node in nodesToRemove)
                {
                    node.Remove();
                }
            }

            // Extract text
            var text = doc.DocumentNode.InnerText;

            // Clean whitespace
            var lines = text.Split('\n')
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line));

            var cleanText = string.Join("\n", lines);

            if (string.IsNullOrWhiteSpace(cleanText))
            {
                AnsiConsole.MarkupLine("[yellow]Warning: No text content found[/]");
                return null;
            }

            // Extract metadata
            var title = doc.DocumentNode.SelectSingleNode("//title")?.InnerText.Trim() ?? "Untitled";
            var description = doc.DocumentNode.SelectSingleNode("//meta[@name='description']")
                ?.GetAttributeValue("content", "") ?? "";

            AnsiConsole.MarkupLine($"[green]Fetched {cleanText.Length} characters[/]");
            AnsiConsole.MarkupLine($"[dim]Title: {title}[/]");

            return new DocumentContent
            {
                Url = url,
                Title = title,
                Description = description,
                Text = cleanText,
                FetchedAt = DateTime.UtcNow
            };
        }
        catch (HttpRequestException ex)
        {
            AnsiConsole.MarkupLine($"[red]HTTP Error:[/] {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return null;
        }
    }
}

public class DocumentContent
{
    public required string Url { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public required string Text { get; set; }
    public DateTime FetchedAt { get; set; }
}
