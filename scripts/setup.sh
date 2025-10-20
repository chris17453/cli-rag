#!/bin/bash
# CLI-RAG Setup Script for Linux

set -e

echo "==================================="
echo "CLI-RAG Setup"
echo "==================================="
echo ""

# Check if .NET SDK is installed
if ! command -v dotnet &> /dev/null; then
    echo "❌ .NET SDK not found"
    echo ""
    echo "Please install .NET 8 SDK:"
    echo ""
    echo "For Fedora/RHEL:"
    echo "  sudo dnf install dotnet-sdk-8.0"
    echo ""
    echo "For Ubuntu/Debian:"
    echo "  wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb"
    echo "  sudo dpkg -i packages-microsoft-prod.deb"
    echo "  sudo apt-get update"
    echo "  sudo apt-get install -y dotnet-sdk-8.0"
    echo ""
    exit 1
fi

echo "✓ .NET SDK found: $(dotnet --version)"
echo ""

# Check GCC version
if command -v gcc &> /dev/null; then
    GCC_VERSION=$(gcc -dumpversion | cut -d. -f1)
    echo "✓ GCC version: $GCC_VERSION"

    if [ "$GCC_VERSION" -eq 15 ]; then
        echo "⚠️  Warning: GCC 15 may have CUDA compatibility issues"
        echo "   Consider using GCC 14 for CUDA support"
    fi
fi
echo ""

# Create directories
echo "Creating directories..."
mkdir -p ~/.cli-rag/models
mkdir -p ~/.cli-rag
echo "✓ Directories created"
echo ""

# Build project
echo "Building project..."
if [ "$1" == "--with-cuda" ]; then
    echo "Building with CUDA support..."
    dotnet build -c Release -p:EnableCuda=true
else
    echo "Building CPU-only version..."
    dotnet build -c Release
fi
echo "✓ Build complete"
echo ""

# Download model (optional)
echo "Model Setup:"
echo "------------"
echo "To use CLI-RAG, you need to download a GGUF model."
echo ""
echo "Recommended models:"
echo "  1. Gemma 2 2B (2GB, fast): "
echo "     wget -P ~/.cli-rag/models https://huggingface.co/bartowski/gemma-2-2b-it-GGUF/resolve/main/gemma-2-2b-it-Q5_K_M.gguf"
echo ""
echo "  2. Llama 3.2 3B (3GB, better quality):"
echo "     wget -P ~/.cli-rag/models https://huggingface.co/bartowski/Llama-3.2-3B-Instruct-GGUF/resolve/main/Llama-3.2-3B-Instruct-Q5_K_M.gguf"
echo ""

if command -v wget &> /dev/null; then
    read -p "Download Gemma 2 2B model now? (y/N): " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        echo "Downloading Gemma 2 2B (~2GB)..."
        wget -P ~/.cli-rag/models \
            https://huggingface.co/bartowski/gemma-2-2b-it-GGUF/resolve/main/gemma-2-2b-it-Q5_K_M.gguf
        echo "✓ Model downloaded"

        # Create config
        cat > ~/.cli-rag/config.json <<EOF
{
  "ModelPath": "~/.cli-rag/models/gemma-2-2b-it-Q5_K_M.gguf",
  "EmbeddingModel": "all-MiniLM-L6-v2",
  "UseGpu": true,
  "GpuLayers": 35,
  "ContextSize": 4096,
  "MaxTokens": 512,
  "Temperature": 0.7,
  "TopK": 5
}
EOF
        echo "✓ Config created"
    fi
fi

echo ""
echo "==================================="
echo "✓ Setup Complete!"
echo "==================================="
echo ""
echo "Usage:"
echo "  dotnet run -- ingest <url>      # Ingest a URL"
echo "  dotnet run -- query             # Interactive query mode"
echo "  dotnet run -- list              # List documents"
echo "  dotnet run -- clear             # Clear database"
echo ""
echo "Or create an alias:"
echo "  echo 'alias clirag=\"cd $(pwd) && dotnet run --\"' >> ~/.bashrc"
echo ""
