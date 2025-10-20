#!/bin/bash
# Interactive demo script for ttyvid recording

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

cd "$PROJECT_ROOT"

# Function to type with delay
type_text() {
    local text="$1"
    local delay="${2:-0.05}"
    for ((i=0; i<${#text}; i++)); do
        echo -n "${text:$i:1}"
        sleep "$delay"
    done
    echo
}

clear
echo "╔════════════════════════════════════════════════════════════════╗"
echo "║        CLI-RAG: Local Agentic RAG System Demo                 ║"
echo "╚════════════════════════════════════════════════════════════════╝"
echo
sleep 2

echo "→ Standard RAG Mode"
echo
sleep 1

type_text "./scripts/clirag.sh query"
sleep 1

# Simulate query input
echo -n "> "
sleep 0.5
type_text "What services does Mansfield Energy provide?" 0.03
sleep 2

# Run actual query (standard mode)
echo "What services does Mansfield Energy provide?" | ./scripts/clirag.sh query 2>&1 | head -50

sleep 3
echo
echo "Press Ctrl+D to exit..."
sleep 2

clear
echo "╔════════════════════════════════════════════════════════════════╗"
echo "║    CLI-RAG: Agentic Mode with Reasoning Steps                 ║"
echo "╚════════════════════════════════════════════════════════════════╝"
echo
sleep 2

echo "→ Agentic RAG Mode (with step-by-step reasoning)"
echo
sleep 1

type_text "./scripts/clirag.sh query --agentic --show-reasoning"
sleep 1

echo -n "> "
sleep 0.5
type_text "What services does Mansfield Energy provide?" 0.03
sleep 2

# Run actual query (agentic mode)
echo "What services does Mansfield Energy provide?" | ./scripts/clirag.sh query --agentic --show-reasoning 2>&1 | head -80

sleep 3
echo
echo "╔════════════════════════════════════════════════════════════════╗"
echo "║                    Demo Complete!                              ║"
echo "╚════════════════════════════════════════════════════════════════╝"
sleep 2
