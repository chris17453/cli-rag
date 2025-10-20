using LLama;
using LLama.Common;
using LLama.Native;
using LLama.Sampling;
using CliRag.Config;
using Spectre.Console;

namespace CliRag.LLM;

public class LocalLLM : IDisposable
{
    private readonly LLamaWeights? _model;
    private readonly LLamaContext? _context;
    private readonly AppConfig _config;
    private readonly bool _isAvailable;

    public bool IsAvailable => _isAvailable;

    public LocalLLM(AppConfig config)
    {
        _config = config;

        try
        {
            if (!File.Exists(config.ModelPath))
            {
                AnsiConsole.MarkupLine($"[yellow]LLM model not found at: {config.ModelPath}[/]");
                AnsiConsole.MarkupLine("[yellow]Download a GGUF model to enable question answering[/]");
                AnsiConsole.MarkupLine("[dim]Example: gemma-2-2b-it-Q5_K_M.gguf from HuggingFace[/]");
                _isAvailable = false;
                return;
            }

            AnsiConsole.MarkupLine("[cyan]Loading LLM...[/]");
            AnsiConsole.MarkupLine($"[dim]Model: {Path.GetFileName(config.ModelPath)}[/]");
            AnsiConsole.MarkupLine($"[dim]Device: {(config.UseGpu ? "GPU" : "CPU")}[/]");

            // Suppress llama.cpp verbose output
            NativeLibraryConfig.All.WithLogCallback((level, message) => { });

            var parameters = new ModelParams(config.ModelPath)
            {
                ContextSize = (uint)config.ContextSize,
                GpuLayerCount = config.UseGpu ? config.GpuLayers : 0,
                UseMemorymap = true,
                UseMemoryLock = false
            };

            _model = LLamaWeights.LoadFromFile(parameters);
            _context = _model.CreateContext(parameters);
            _isAvailable = true;

            AnsiConsole.MarkupLine("[green]LLM loaded successfully[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error loading LLM: {ex.Message}[/]");
            _isAvailable = false;
        }
    }

    public async Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (!_isAvailable || _model == null || _context == null)
        {
            return "Error: LLM is not available. Please check your model configuration.";
        }

        try
        {
            var executor = new StatelessExecutor(_model, _context.Params);

            var inferenceParams = new InferenceParams
            {
                MaxTokens = _config.MaxTokens,
                SamplingPipeline = new DefaultSamplingPipeline
                {
                    Temperature = _config.Temperature
                },
                AntiPrompts = new[] { "<|eot_id|>", "</s>", "<|end_of_text|>" }
            };

            var response = "";

            await foreach (var text in executor.InferAsync(prompt, inferenceParams, cancellationToken))
            {
                response += text;
            }

            // Clean up response - remove any remaining special tokens
            response = response
                .Replace("<|eot_id|>", "")
                .Replace("</s>", "")
                .Replace("<|end_of_text|>", "")
                .Trim();

            return response;
        }
        catch (Exception ex)
        {
            return $"Error generating response: {ex.Message}";
        }
    }

    public void Dispose()
    {
        _context?.Dispose();
        _model?.Dispose();
        GC.SuppressFinalize(this);
    }
}
