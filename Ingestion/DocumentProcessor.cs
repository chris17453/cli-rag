using CliRag.Config;

namespace CliRag.Ingestion;

public class DocumentProcessor
{
    private readonly AppConfig _config;

    public DocumentProcessor(AppConfig config)
    {
        _config = config;
    }

    public List<TextChunk> ChunkDocument(DocumentContent document)
    {
        var chunks = new List<TextChunk>();
        var text = document.Text;
        var chunkSize = _config.ChunkSize;
        var overlap = _config.ChunkOverlap;

        // Split by paragraphs first
        var paragraphs = text.Split(new[] { "\n\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        var currentChunk = "";
        var chunkIndex = 0;

        foreach (var paragraph in paragraphs)
        {
            // If adding this paragraph would exceed chunk size
            if (currentChunk.Length + paragraph.Length > chunkSize && currentChunk.Length > 0)
            {
                // Save current chunk
                chunks.Add(new TextChunk
                {
                    Id = $"{document.Url}_{chunkIndex}",
                    Text = currentChunk.Trim(),
                    ChunkIndex = chunkIndex,
                    Url = document.Url,
                    Title = document.Title,
                    Description = document.Description
                });

                chunkIndex++;

                // Start new chunk with overlap
                if (overlap > 0 && currentChunk.Length > overlap)
                {
                    currentChunk = currentChunk.Substring(currentChunk.Length - overlap) + "\n" + paragraph;
                }
                else
                {
                    currentChunk = paragraph;
                }
            }
            else
            {
                if (currentChunk.Length > 0)
                    currentChunk += "\n" + paragraph;
                else
                    currentChunk = paragraph;
            }
        }

        // Add final chunk
        if (!string.IsNullOrWhiteSpace(currentChunk))
        {
            chunks.Add(new TextChunk
            {
                Id = $"{document.Url}_{chunkIndex}",
                Text = currentChunk.Trim(),
                ChunkIndex = chunkIndex,
                Url = document.Url,
                Title = document.Title,
                Description = document.Description
            });
        }

        return chunks;
    }
}

public class TextChunk
{
    public required string Id { get; set; }
    public required string Text { get; set; }
    public int ChunkIndex { get; set; }
    public required string Url { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
}
