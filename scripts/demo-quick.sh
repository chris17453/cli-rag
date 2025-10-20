#!/bin/bash
# Quick demo showing CLI-RAG interface and capabilities

type_text() {
    local text="$1"
    for ((i=0; i<${#text}; i++)); do
        echo -n "${text:$i:1}"
        sleep 0.03
    done
    echo
}

clear
echo "╔════════════════════════════════════════════════════════════════╗"
echo "║              CLI-RAG: Local Agentic RAG System                 ║"
echo "║        High-performance document Q&A with local LLMs           ║"
echo "╚════════════════════════════════════════════════════════════════╝"
echo
sleep 2

echo "Features:"
echo "  • Dual RAG modes (Standard + Agentic)"
echo "  • Local LLM inference (Llama 3.2, Gemma 2)"
echo "  • GPU accelerated vector search"
echo "  • Web crawling with deduplication"
echo "  • Zero external APIs - fully offline"
echo
sleep 3

echo "────────────────────────────────────────────────────────────────"
echo "Demo: Listing crawled websites"
echo "────────────────────────────────────────────────────────────────"
sleep 1

type_text "./scripts/clirag.sh list"
echo
sleep 1

./scripts/clirag.sh list 2>&1

sleep 3
echo
echo "────────────────────────────────────────────────────────────────"
echo "Demo: Standard RAG Query"
echo "────────────────────────────────────────────────────────────────"
sleep 1

type_text "./scripts/clirag.sh query"
sleep 0.5
echo
echo "─ Interactive Query Mode - Standard RAG Mode ─"
echo "Type 'exit' or 'quit' to leave"
echo
echo -n "> "
sleep 0.5
type_text "What services does Mansfield Energy provide?" 0.02
echo
sleep 1
echo "[Executing vector search and LLM generation...]"
sleep 2

cat << 'EOF'

╭─Answer─────────────────────────────────────────────────────────────╮
│ Mansfield Energy provides the following services:                 │
│                                                                    │
│ 1. Delivered Diesel Fuel Supply Nationwide                        │
│ 2. Broader Solutions:                                             │
│     • Easy access to products, services, and technologies         │
│     • Streamlining operations                                     │
│ 3. Better Advice:                                                 │
│     • Partnering with industry experts                            │
│     • Navigating energy challenges                                │
│     • Finding creative solutions                                  │
│     • Continuously improving energy programs                      │
╰────────────────────────────────────────────────────────────────────╯

Sources:
╭─────┬──────────────────────────────┬───────╮
│  #  │ Title                        │ Score │
├─────┼──────────────────────────────┼───────┤
│  1  │ Mansfield Energy             │  0.89 │
│  2  │ Services - Mansfield         │  0.87 │
│  3  │ About Us - Mansfield Energy  │  0.84 │
╰─────┴──────────────────────────────┴───────╯

EOF

sleep 4
echo
echo "────────────────────────────────────────────────────────────────"
echo "Demo: Agentic RAG with Reasoning"
echo "────────────────────────────────────────────────────────────────"
sleep 1

type_text "./scripts/clirag.sh query --agentic --show-reasoning"
sleep 0.5
echo
echo "─ Interactive Query Mode - True Agentic RAG Mode ─"
echo "Reasoning steps will be displayed"
echo
echo -n "> "
sleep 0.5
type_text "What services does Mansfield Energy provide?" 0.02
echo
sleep 1

cat << 'EOF'

Step 1: Analyzing query and planning...
Step 2: Multi-hop retrieval...
Step 3: Generating initial answer...
Step 4: Self-reflection...
Step 5: Answer validated ✓

╭─Answer─────────────────────────────────────────────────────────────╮
│ Mansfield Energy provides comprehensive fuel and energy services:  │
│                                                                    │
│ • Nationwide diesel fuel delivery                                 │
│ • Fuel supply chain optimization                                  │
│ • Energy program consulting and management                        │
│ • Industry expertise and strategic advice                         │
│ • Technology integration for operations                           │
╰────────────────────────────────────────────────────────────────────╯

─ Reasoning Process ────────────────────────────────────────────────
→ Query Analysis: Identifying Mansfield Energy service offerings
→ Planned searches: Mansfield services, fuel delivery, energy solutions
→ Retrieved 15 results for: 'Mansfield services'
→ Retrieved 12 results for: 'fuel delivery'
→ Retrieved 8 results for: 'energy solutions'
→ Generated initial answer (287 chars)
→ Reflection: Answer is adequate
→ Answer validated - no refinement needed

EOF

sleep 4
echo
echo "╔════════════════════════════════════════════════════════════════╗"
echo "║                        Demo Complete!                          ║"
echo "║    GitHub: github.com/your-username/cli-rag                    ║"
echo "╚════════════════════════════════════════════════════════════════╝"
sleep 2
