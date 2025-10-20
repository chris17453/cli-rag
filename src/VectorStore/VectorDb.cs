using Microsoft.Data.Sqlite;
using Dapper;
using CliRag.Config;
using CliRag.Ingestion;
using CliRag.Embeddings;
using Spectre.Console;
using System.Text.Json;

namespace CliRag.VectorStore;

public class VectorDb : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly LocalEmbeddings _embeddings;
    private readonly AppConfig _config;

    public VectorDb(AppConfig config, LocalEmbeddings embeddings)
    {
        _config = config;
        _embeddings = embeddings;
        _connection = new SqliteConnection($"Data Source={config.DatabasePath}");
        _connection.Open();
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        var sql = @"
            CREATE TABLE IF NOT EXISTS documents (
                id TEXT PRIMARY KEY,
                url TEXT NOT NULL,
                title TEXT,
                description TEXT,
                text TEXT NOT NULL,
                chunk_index INTEGER NOT NULL,
                embedding BLOB,
                created_at TEXT DEFAULT CURRENT_TIMESTAMP
            );

            CREATE INDEX IF NOT EXISTS idx_url ON documents(url);
            CREATE INDEX IF NOT EXISTS idx_chunk ON documents(url, chunk_index);

            CREATE TABLE IF NOT EXISTS crawled_urls (
                base_url TEXT PRIMARY KEY,
                crawled_at TEXT DEFAULT CURRENT_TIMESTAMP,
                page_count INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS ingested_urls (
                url TEXT PRIMARY KEY,
                title TEXT,
                ingested_at TEXT DEFAULT CURRENT_TIMESTAMP,
                chunk_count INTEGER NOT NULL
            );
        ";

        _connection.Execute(sql);

        // Migrate old JSON embeddings to BLOB if needed
        MigrateEmbeddingsToBlob();
    }

    private void MigrateEmbeddingsToBlob()
    {
        try
        {
            // Check if there are any TEXT embeddings that need migration
            var sql = "SELECT id, embedding FROM documents WHERE embedding IS NOT NULL AND typeof(embedding) = 'text' LIMIT 1";
            var needsMigration = _connection.Query(sql).Any();

            if (needsMigration)
            {
                AnsiConsole.MarkupLine("[yellow]Migrating embeddings to optimized format...[/]");

                var allDocs = _connection.Query<(string id, string embedding)>(
                    "SELECT id, embedding FROM documents WHERE embedding IS NOT NULL AND typeof(embedding) = 'text'"
                ).ToList();

                foreach (var doc in allDocs)
                {
                    var floatArray = JsonSerializer.Deserialize<float[]>(doc.embedding);
                    if (floatArray != null)
                    {
                        var blob = SerializeEmbedding(floatArray);
                        _connection.Execute(
                            "UPDATE documents SET embedding = @Blob WHERE id = @Id",
                            new { Id = doc.id, Blob = blob }
                        );
                    }
                }

                AnsiConsole.MarkupLine("[green]Migration complete![/]");
            }
        }
        catch
        {
            // Migration failed or not needed, continue
        }
    }

    public async Task AddChunksAsync(List<TextChunk> chunks)
    {
        AnsiConsole.MarkupLine($"[cyan]Processing {chunks.Count} chunks...[/]");

        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[cyan]Generating embeddings...[/]", maxValue: chunks.Count);

                // Use transaction for batch insert
                using var transaction = _connection.BeginTransaction();

                try
                {
                    foreach (var chunk in chunks)
                    {
                        byte[]? embeddingBlob = null;
                        if (_embeddings.IsAvailable)
                        {
                            var embedding = await _embeddings.EmbedTextAsync(chunk.Text);
                            if (embedding != null)
                            {
                                embeddingBlob = SerializeEmbedding(embedding);
                            }
                        }

                        var sql = @"
                            INSERT OR REPLACE INTO documents
                            (id, url, title, description, text, chunk_index, embedding)
                            VALUES (@Id, @Url, @Title, @Description, @Text, @ChunkIndex, @Embedding)
                        ";

                        await _connection.ExecuteAsync(sql, new
                        {
                            chunk.Id,
                            chunk.Url,
                            chunk.Title,
                            chunk.Description,
                            chunk.Text,
                            chunk.ChunkIndex,
                            Embedding = embeddingBlob
                        }, transaction);

                        task.Increment(1);
                    }

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            });

        AnsiConsole.MarkupLine($"[green]Added {chunks.Count} chunks to database[/]");
    }

    public async Task<List<SearchResult>> SearchAsync(string query, int topK)
    {
        var results = new List<SearchResult>();

        if (_embeddings.IsAvailable)
        {
            // Vector similarity search
            var queryEmbedding = await _embeddings.EmbedTextAsync(query);
            if (queryEmbedding != null)
            {
                results = await VectorSearchAsync(queryEmbedding, topK);
            }
        }

        // Fallback to keyword search if no vector results
        if (results.Count == 0)
        {
            results = await KeywordSearchAsync(query, topK);
        }

        return results;
    }

    private async Task<List<SearchResult>> VectorSearchAsync(float[] queryEmbedding, int topK)
    {
        var sql = "SELECT id, url, title, text, chunk_index, embedding FROM documents WHERE embedding IS NOT NULL";
        var docs = await _connection.QueryAsync<DocumentRow>(sql);

        var scoredDocs = new List<(DocumentRow doc, float score)>();

        foreach (var doc in docs)
        {
            if (doc.Embedding == null || doc.Embedding.Length == 0)
                continue;

            var docEmbedding = DeserializeEmbedding(doc.Embedding);

            var similarity = CosineSimilarity(queryEmbedding, docEmbedding);

            if (similarity >= _config.SimilarityThreshold)
            {
                scoredDocs.Add((doc, similarity));
            }
        }

        return scoredDocs
            .OrderByDescending(x => x.score)
            .Take(topK)
            .Select(x => new SearchResult
            {
                Text = x.doc.Text,
                Url = x.doc.Url,
                Title = x.doc.Title,
                ChunkIndex = x.doc.ChunkIndex,
                Score = x.score
            })
            .ToList();
    }

    private async Task<List<SearchResult>> KeywordSearchAsync(string query, int topK)
    {
        var keywords = query.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var sql = "SELECT id, url, title, text, chunk_index FROM documents";
        var docs = await _connection.QueryAsync<DocumentRow>(sql);

        var scoredDocs = docs.Select(doc =>
        {
            var textLower = doc.Text.ToLower();
            var score = keywords.Sum(k => textLower.Contains(k) ? 1 : 0) / (float)keywords.Length;
            return (doc, score);
        })
        .Where(x => x.score > 0)
        .OrderByDescending(x => x.score)
        .Take(topK)
        .Select(x => new SearchResult
        {
            Text = x.doc.Text,
            Url = x.doc.Url,
            Title = x.doc.Title,
            ChunkIndex = x.doc.ChunkIndex,
            Score = x.score
        })
        .ToList();

        return scoredDocs;
    }

    public async Task<List<string>> ListDocumentsAsync()
    {
        var sql = "SELECT DISTINCT url, title FROM documents ORDER BY url";
        var docs = await _connection.QueryAsync<(string url, string title)>(sql);
        return docs.Select(d => $"{d.title} ({d.url})").ToList();
    }

    public async Task DeleteDocumentAsync(string url)
    {
        var sql = "DELETE FROM documents WHERE url = @Url";
        var affected = await _connection.ExecuteAsync(sql, new { Url = url });
        AnsiConsole.MarkupLine($"[green]Deleted {affected} chunks for URL[/]");
    }

    public async Task ClearAllAsync()
    {
        var count = await _connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM documents");
        await _connection.ExecuteAsync("DELETE FROM documents");
        await _connection.ExecuteAsync("DELETE FROM crawled_urls");
        await _connection.ExecuteAsync("DELETE FROM ingested_urls");
        AnsiConsole.MarkupLine($"[green]Cleared {count} chunks from database[/]");
    }

    public async Task<bool> IsUrlCrawledAsync(string baseUrl)
    {
        var sql = "SELECT COUNT(*) FROM crawled_urls WHERE base_url = @BaseUrl";
        var count = await _connection.ExecuteScalarAsync<int>(sql, new { BaseUrl = baseUrl });
        return count > 0;
    }

    public async Task MarkUrlCrawledAsync(string baseUrl, int pageCount)
    {
        var sql = @"
            INSERT OR REPLACE INTO crawled_urls (base_url, page_count, crawled_at)
            VALUES (@BaseUrl, @PageCount, datetime('now'))
        ";
        await _connection.ExecuteAsync(sql, new { BaseUrl = baseUrl, PageCount = pageCount });
    }

    public async Task<List<CrawledUrl>> ListCrawledUrlsAsync()
    {
        var sql = "SELECT base_url, crawled_at, page_count FROM crawled_urls ORDER BY crawled_at DESC";
        var urls = await _connection.QueryAsync<CrawledUrl>(sql);
        return urls.ToList();
    }

    public async Task<bool> IsUrlIngestedAsync(string url)
    {
        var sql = "SELECT COUNT(*) FROM ingested_urls WHERE url = @Url";
        var count = await _connection.ExecuteScalarAsync<int>(sql, new { Url = url });
        return count > 0;
    }

    public async Task MarkUrlIngestedAsync(string url, string title, int chunkCount)
    {
        var sql = @"
            INSERT OR REPLACE INTO ingested_urls (url, title, chunk_count, ingested_at)
            VALUES (@Url, @Title, @ChunkCount, datetime('now'))
        ";
        await _connection.ExecuteAsync(sql, new { Url = url, Title = title, ChunkCount = chunkCount });
    }

    public async Task<List<IngestedUrl>> ListIngestedUrlsAsync()
    {
        var sql = "SELECT url, title, ingested_at, chunk_count FROM ingested_urls ORDER BY ingested_at DESC";
        var urls = await _connection.QueryAsync<IngestedUrl>(sql);
        return urls.ToList();
    }

    private static byte[] SerializeEmbedding(float[] embedding)
    {
        var bytes = new byte[embedding.Length * sizeof(float)];
        Buffer.BlockCopy(embedding, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static float[] DeserializeEmbedding(byte[] bytes)
    {
        var floats = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
        return floats;
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            return 0;

        float dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        return (float)(dot / (Math.Sqrt(normA) * Math.Sqrt(normB)));
    }

    public void Dispose()
    {
        _connection?.Dispose();
        GC.SuppressFinalize(this);
    }

    private class DocumentRow
    {
        public string Id { get; set; } = "";
        public string Url { get; set; } = "";
        public string Title { get; set; } = "";
        public string Text { get; set; } = "";
        public int ChunkIndex { get; set; }
        public byte[]? Embedding { get; set; }
    }
}

public class SearchResult
{
    public required string Text { get; set; }
    public required string Url { get; set; }
    public required string Title { get; set; }
    public int ChunkIndex { get; set; }
    public float Score { get; set; }
}

public class CrawledUrl
{
    public string Base_Url { get; set; } = "";
    public string Crawled_At { get; set; } = "";
    public int Page_Count { get; set; }
}

public class IngestedUrl
{
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public string Ingested_At { get; set; } = "";
    public int Chunk_Count { get; set; }
}
