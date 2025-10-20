using System.Text.Json;

namespace CliRag.Config;

public class AppConfig
{
    public string ModelPath { get; set; } = "~/.cli-rag/models/gemma-2-2b-it-Q5_K_M.gguf";
    public string EmbeddingModel { get; set; } = "all-MiniLM-L6-v2";
    public string DatabasePath { get; set; } = "~/.cli-rag/vectors.db";
    public bool UseGpu { get; set; } = true;
    public int GpuLayers { get; set; } = 35;
    public int ContextSize { get; set; } = 4096;
    public int MaxTokens { get; set; } = 512;
    public float Temperature { get; set; } = 0.7f;
    public int TopK { get; set; } = 5;
    public int ChunkSize { get; set; } = 500;
    public int ChunkOverlap { get; set; } = 50;
    public float SimilarityThreshold { get; set; } = 0.7f;

    private static string ConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".cli-rag", "config.json");

    public static AppConfig Load()
    {
        var config = new AppConfig();

        if (File.Exists(ConfigPath))
        {
            try
            {
                var json = File.ReadAllText(ConfigPath);
                var loaded = JsonSerializer.Deserialize<AppConfig>(json);
                if (loaded != null)
                    config = loaded;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not load config: {ex.Message}");
            }
        }

        // Expand paths
        config.ModelPath = ExpandPath(config.ModelPath);
        config.DatabasePath = ExpandPath(config.DatabasePath);

        // Ensure directories exist
        EnsureDirectoryExists(config.ModelPath);
        EnsureDirectoryExists(config.DatabasePath);

        return config;
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(ConfigPath);
            if (dir != null)
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not save config: {ex.Message}");
        }
    }

    private static string ExpandPath(string path)
    {
        if (path.StartsWith("~/"))
        {
            path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                path.Substring(2));
        }
        return Path.GetFullPath(path);
    }

    private static void EnsureDirectoryExists(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (dir != null)
            Directory.CreateDirectory(dir);
    }
}
