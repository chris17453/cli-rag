#!/bin/bash
# Demo script for ttyvid recording

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

cd "$PROJECT_ROOT"

echo "=== CLI-RAG Demo: Standard RAG Mode ==="
echo ""
sleep 1

# Standard mode query
./scripts/clirag.sh query <<EOF
What services does Mansfield Energy provide?
exit
EOF

echo ""
echo "=== CLI-RAG Demo: Agentic RAG Mode with Reasoning ==="
echo ""
sleep 2

# Agentic mode with reasoning
./scripts/clirag.sh query --agentic --show-reasoning <<EOF
What services does Mansfield Energy provide?
exit
EOF

echo ""
echo "Demo complete!"
