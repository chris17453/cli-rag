#!/bin/bash
# Test script for interactive query

dotnet run --no-build -- query <<EOF
What is RAG?
exit
EOF
