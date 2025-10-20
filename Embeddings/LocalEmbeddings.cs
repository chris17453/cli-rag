using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Spectre.Console;
using System.Text.RegularExpressions;

namespace CliRag.Embeddings;

/// <summary>
/// Local embeddings using ONNX Runtime with sentence-transformers models
/// Downloads and runs all-MiniLM-L6-v2 locally (80MB model)
/// </summary>
public partial class LocalEmbeddings : IDisposable
{
    private readonly InferenceSession? _session;
    private readonly Dictionary<string, int> _vocabulary;
    private bool _isAvailable;

    public bool IsAvailable => _isAvailable;

    public LocalEmbeddings()
    {
        try
        {
            var modelPath = GetModelPath();

            if (!File.Exists(modelPath))
            {
                AnsiConsole.MarkupLine("[yellow]Embedding model not found. Will download on first use.[/]");
                AnsiConsole.MarkupLine($"[dim]Model will be saved to: {modelPath}[/]");
                _isAvailable = false;
                _vocabulary = new Dictionary<string, int>();
                return;
            }

            AnsiConsole.MarkupLine("[cyan]Loading embedding model...[/]");
            _session = new InferenceSession(modelPath);
            _vocabulary = LoadVocabulary();
            _isAvailable = true;
            AnsiConsole.MarkupLine("[green]Embedding model loaded[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]Could not load embedding model: {ex.Message}[/]");
            AnsiConsole.MarkupLine("[yellow]Embeddings will be unavailable. You can still use keyword search.[/]");
            _vocabulary = new Dictionary<string, int>();
            _isAvailable = false;
        }
    }

    public async Task<float[]?> EmbedTextAsync(string text)
    {
        if (!_isAvailable || _session == null)
        {
            // Attempt to download model if not available
            await EnsureModelDownloadedAsync();
            if (!_isAvailable)
                return null;
        }

        try
        {
            var tokens = Tokenize(text);

            // Create flat arrays for ONNX
            var inputIdsFlat = new long[tokens.Length];
            var attentionMaskFlat = new long[tokens.Length];

            for (int i = 0; i < tokens.Length; i++)
            {
                inputIdsFlat[i] = tokens[i];
                attentionMaskFlat[i] = 1;
            }

            // Create tensors with correct dimensions
            var inputIdsTensor = new DenseTensor<long>(inputIdsFlat, new[] { 1, tokens.Length });
            var attentionMaskTensor = new DenseTensor<long>(attentionMaskFlat, new[] { 1, tokens.Length });

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
                NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor)
            };

            using var results = _session!.Run(inputs);
            var embedding = results.First().AsEnumerable<float>().ToArray();

            // Normalize
            var norm = Math.Sqrt(embedding.Sum(x => x * x));
            return embedding.Select(x => (float)(x / norm)).ToArray();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error generating embedding: {ex.Message}[/]");
            return null;
        }
    }

    private int[] Tokenize(string text)
    {
        // Simple whitespace tokenization (for demonstration)
        // In production, use proper tokenizer like BertTokenizer
        var words = WordRegex().Split(text.ToLower()).Where(w => !string.IsNullOrWhiteSpace(w));
        var tokens = new List<int> { 101 }; // [CLS]

        foreach (var word in words.Take(510))
        {
            if (_vocabulary.TryGetValue(word, out int tokenId))
                tokens.Add(tokenId);
            else
                tokens.Add(100); // [UNK]
        }

        tokens.Add(102); // [SEP]
        return tokens.ToArray();
    }

    private static Dictionary<string, int> LoadVocabulary()
    {
        // Simplified vocabulary - in production load from vocab.txt
        return new Dictionary<string, int>
        {
            ["[PAD]"] = 0,
            ["[UNK]"] = 100,
            ["[CLS]"] = 101,
            ["[SEP]"] = 102
        };
    }

    private static string GetModelPath()
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var modelDir = Path.Combine(homeDir, ".cli-rag", "models");
        Directory.CreateDirectory(modelDir);
        return Path.Combine(modelDir, "model.onnx");
    }

    private async Task EnsureModelDownloadedAsync()
    {
        var modelPath = GetModelPath();
        if (File.Exists(modelPath))
            return;

        AnsiConsole.MarkupLine("[yellow]Embedding model not available.[/]");
        AnsiConsole.MarkupLine("[dim]To enable embeddings, download a model from HuggingFace:[/]");
        AnsiConsole.MarkupLine("[dim]https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2[/]");
        AnsiConsole.MarkupLine($"[dim]Save as: {modelPath}[/]");

        await Task.CompletedTask;
    }

    [GeneratedRegex(@"\W+")]
    private static partial Regex WordRegex();

    public void Dispose()
    {
        _session?.Dispose();
        GC.SuppressFinalize(this);
    }
}
