#!/bin/bash
# CLI-RAG wrapper script with CUDA 12.8 support

# Get project root (one level up from scripts/)
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

# Set library paths for CUDA 12.8 and llama.cpp CUDA backend
export LD_LIBRARY_PATH="/usr/local/cuda-12.8/lib64:${PROJECT_ROOT}/build/bin/Release/net8.0/runtimes/linux-x64/native/cuda12:$LD_LIBRARY_PATH"

# Run the application
exec "${PROJECT_ROOT}/build/bin/Release/net8.0/CliRag" "$@"
