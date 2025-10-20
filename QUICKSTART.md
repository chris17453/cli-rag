# Quick Start Guide

Get up and running with CLI-RAG in 5 minutes.

## Prerequisites

- **Linux** (Fedora, Ubuntu, or similar)
- **.NET 8 SDK** or later
- **GCC 14** (for CUDA compatibility)
- **CUDA 12.8** (optional, for GPU acceleration)

## Installation

### 1. Install .NET 8 SDK

<details>
<summary><b>Fedora/RHEL</b></summary>

```bash
sudo dnf install dotnet-sdk-8.0
```
</details>

<details>
<summary><b>Ubuntu/Debian</b></summary>

```bash
wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt-get update && sudo apt-get install -y dotnet-sdk-8.0
```
</details>

### 2. Clone and Build

```bash
git clone https://github.com/yourusername/cli-rag.git
cd cli-rag
dotnet build -c Release
chmod +x clirag.sh
```

### 3. Download a Model

Choose one based on your hardware:

**Llama 3.2 3B (Recommended - 1.7GB)**
```bash
mkdir -p ~/.cli-rag/models
wget https://huggingface.co/bartowski/Llama-3.2-3B-Instruct-GGUF/resolve/main/Llama-3.2-3B-Instruct-Q4_K_M.gguf \
  -O ~/.cli-rag/models/Llama-3.2-3B-Instruct-Q4_K_M.gguf
```

**Llama 3.2 1B (Fastest - 700MB)**
```bash
wget https://huggingface.co/bartowski/Llama-3.2-1B-Instruct-GGUF/resolve/main/Llama-3.2-1B-Instruct-Q4_K_M.gguf \
  -O ~/.cli-rag/models/Llama-3.2-1B-Instruct-Q4_K_M.gguf
```

**Gemma 2 2B (Alternative - 1.5GB)**
```bash
wget https://huggingface.co/bartowski/gemma-2-2b-it-GGUF/resolve/main/gemma-2-2b-it-Q5_K_M.gguf \
  -O ~/.cli-rag/models/gemma-2-2b-it-Q5_K_M.gguf
```

### 4. Configure (Optional)

Create `~/.cli-rag/config.json`:

```json
{
  "ModelPath": "~/.cli-rag/models/Llama-3.2-3B-Instruct-Q4_K_M.gguf",
  "EmbeddingModel": "all-MiniLM-L6-v2",
  "UseGpu": true,
  "GpuLayers": 33,
  "ContextSize": 4096,
  "MaxTokens": 256,
  "Temperature": 0.3,
  "TopK": 5
}
```

**GPU Settings:**
| VRAM | GpuLayers | Model Recommendation |
|------|-----------|----------------------|
| No GPU | 0 | Llama 3.2 1B |
| 4GB | 20 | Llama 3.2 3B |
| 8GB+ | 33 | Llama 3.2 3B |
| 16GB+ | 60 | Gemma 2 9B |

## Basic Usage

### Ingest a Single URL

```bash
./clirag.sh ingest https://en.wikipedia.org/wiki/Retrieval-augmented_generation
```

Output:
```
─ Ingesting URL ─────────────────────────────
Fetching URL: https://en.wikipedia.org/wiki/Retrieval-augmented_generation
Fetched 45,234 characters
Title: Retrieval-augmented generation
Created 92 chunks

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ 100% 0:00:03
Added 92 chunks to database
─ Ingestion Complete ────────────────────────
```

### Crawl an Entire Website

```bash
./clirag.sh crawl https://docs.example.com --max-pages 50
```

This will:
- Stay within the same domain
- Respect 500ms delay between requests
- Show progress bar
- Prevent re-crawling (tracked in database)

### Ask Questions

**Standard RAG Mode:**
```bash
./clirag.sh query
```

```
─ Interactive Query Mode - Standard RAG Mode ─
Type 'exit' or 'quit' to leave

> What is RAG?

╭─Answer──────────────────────────────────────────────────────╮
│ Retrieval-Augmented Generation (RAG) is a technique that    │
│ combines information retrieval with text generation. It     │
│ retrieves relevant documents from a knowledge base and      │
│ uses them as context for a language model to generate       │
│ more accurate and grounded responses.                       │
╰─────────────────────────────────────────────────────────────╯

Sources:
╭─────┬──────────────────────────────────────────┬───────╮
│  #  │ Title                                    │ Score │
├─────┼──────────────────────────────────────────┼───────┤
│  1  │ Retrieval-augmented generation           │  0.89 │
│  2  │ Retrieval-augmented generation           │  0.87 │
│  3  │ Retrieval-augmented generation           │  0.82 │
╰─────┴──────────────────────────────────────────┴───────╯

> exit
```

**Agentic RAG Mode with Reasoning:**
```bash
./clirag.sh query --agentic --show-reasoning
```

```
─ Interactive Query Mode - True Agentic RAG Mode ─
Reasoning steps will be displayed

> What are the main benefits of RAG compared to fine-tuning?

Step 1: Analyzing query and planning...
Step 2: Multi-hop retrieval...
Step 3: Generating initial answer...
Step 4: Self-reflection...
Step 5: Answer validated ✓

╭─Answer──────────────────────────────────────────────────────╮
│ RAG offers several key benefits over fine-tuning:           │
│                                                             │
│ 1. **Dynamic Knowledge**: RAG can access up-to-date info    │
│    without retraining the model                             │
│                                                             │
│ 2. **Lower Cost**: No expensive GPU training required       │
│                                                             │
│ 3. **Transparency**: Can cite sources and show retrieved    │
│    documents                                                │
│                                                             │
│ 4. **Flexibility**: Easy to update knowledge base without   │
│    model changes                                            │
╰─────────────────────────────────────────────────────────────╯

─ Reasoning Process ─────────────────────────────────────────
→ Query Analysis: Comparing RAG and fine-tuning approaches
→ Planned searches: RAG benefits, fine-tuning limitations, knowledge updates
→ Retrieved 15 results for: 'RAG benefits'
→ Retrieved 12 results for: 'fine-tuning limitations'
→ Retrieved 8 results for: 'knowledge updates'
→ Generated initial answer (320 chars)
→ Reflection: Answer is adequate
→ Answer validated - no refinement needed
```

### List All URLs

```bash
./clirag.sh list
```

Output shows both ingested URLs and crawled sites:
```
─ All URLs ──────────────────────────────────

Ingested URLs:
╭──────────────────────────────────────┬──────────┬────────┬─────────────────╮
│ URL                                  │ Title    │ Chunks │ Ingested At     │
├──────────────────────────────────────┼──────────┼────────┼─────────────────┤
│ https://en.wikipedia.org/wiki/RAG    │ RAG Wiki │ 92     │ 2025-10-20 ...  │
╰──────────────────────────────────────┴──────────┴────────┴─────────────────╯

Crawled Websites:
╭──────────────────────────┬───────┬─────────────────╮
│ Base URL                 │ Pages │ Crawled At      │
├──────────────────────────┼───────┼─────────────────┤
│ https://docs.example.com │ 45    │ 2025-10-20 ...  │
╰──────────────────────────┴───────┴─────────────────╯
```

### Clear Database

```bash
# With confirmation
./clirag.sh clear

# Skip confirmation
./clirag.sh clear --force
```

## Advanced Usage

### Batch Ingestion

```bash
# Ingest multiple URLs
./clirag.sh ingest https://example.com/doc1
./clirag.sh ingest https://example.com/doc2
./clirag.sh ingest https://example.com/doc3

# Or use a loop
for url in $(cat urls.txt); do
  ./clirag.sh ingest "$url"
done
```

### Export Configuration

Save your working config:
```bash
cat ~/.cli-rag/config.json > config-backup.json
```

### Check Database Size

```bash
du -h ~/.cli-rag/vectors.db
```

## Troubleshooting

### GPU Not Detected

```bash
# Check CUDA
nvidia-smi

# Check library paths
ldd ./bin/Release/net8.0/CliRag | grep cuda

# The wrapper script should handle paths
./clirag.sh query
```

### Out of Memory

Lower the GPU layers in config:
```json
{
  "GpuLayers": 15
}
```

Or use CPU-only:
```json
{
  "UseGpu": false,
  "GpuLayers": 0
}
```

### Slow Queries

1. Check database size:
   ```bash
   du -h ~/.cli-rag/vectors.db
   ```

2. Reduce TopK (fewer chunks retrieved):
   ```json
   {
     "TopK": 3
   }
   ```

3. Use a smaller model (Llama 1B instead of 3B)

### Model Load Errors

Verify model path:
```bash
ls -lh ~/.cli-rag/models/
cat ~/.cli-rag/config.json | grep ModelPath
```

### Embeddings Not Working

The system will automatically fall back to keyword search if ONNX embeddings fail. Check console output for warnings.

## Performance Tips

### For Speed
- Use Q4 quantization (smaller, faster)
- Lower `MaxTokens` to 128-256
- Reduce `TopK` to 3
- Use Llama 3.2 1B model
- Enable GPU offloading

### For Quality
- Use Q5 or Q6 quantization
- Increase `MaxTokens` to 512+
- Increase `TopK` to 7-10
- Use agentic mode with reasoning
- Use Llama 3.2 3B or Gemma 2 2B

### For Memory Efficiency
- Use CPU-only mode
- Lower `ContextSize` to 2048
- Use Q4 quantization
- Use smaller models (1B)

## Performance Expectations

**Llama 3.2 1B Q4:**
| Hardware | Tokens/sec | Query Time |
|----------|------------|------------|
| CPU (8-core) | ~25 | ~6s |
| GPU (RTX 3060) | ~120 | ~2s |

**Llama 3.2 3B Q4:**
| Hardware | Tokens/sec | Query Time |
|----------|------------|------------|
| CPU (8-core) | ~15 | ~10s |
| GPU (RTX 3060) | ~80 | ~3s |

**Gemma 2 2B Q5:**
| Hardware | Tokens/sec | Query Time |
|----------|------------|------------|
| CPU (8-core) | ~20 | ~8s |
| GPU (RTX 3060) | ~100 | ~2.5s |

## Creating a System Alias

Add to `~/.bashrc` or `~/.zshrc`:

```bash
alias rag="cd /path/to/cli-rag && ./clirag.sh"
```

Then use:
```bash
rag ingest https://example.com
rag query --agentic
rag list
```

## Next Steps

1. **Ingest your documentation** - Crawl your project docs
2. **Try agentic mode** - See multi-step reasoning in action
3. **Experiment with models** - Find the best speed/quality tradeoff
4. **Integrate with workflows** - Use in scripts, CI/CD, etc.
5. **Read ARCHITECTURE.md** - Understand the internals

## Common Workflows

**Daily Documentation Q&A:**
```bash
# Morning: crawl updated docs
./clirag.sh crawl https://docs.myproject.com --max-pages 100

# Throughout day: ask questions
./clirag.sh query --agentic
```

**Research Assistant:**
```bash
# Ingest research papers
./clirag.sh ingest https://arxiv.org/pdf/1234.5678
./clirag.sh ingest https://arxiv.org/pdf/9876.5432

# Query with reasoning
./clirag.sh query --agentic --show-reasoning
```

**Code Documentation Helper:**
```bash
# Crawl API docs
./clirag.sh crawl https://api.example.com/docs

# Quick lookups
./clirag.sh query
> How do I authenticate with the API?
```

---

**Need help?** Check the [README](README.md) or [ARCHITECTURE](ARCHITECTURE.md) for more details.
