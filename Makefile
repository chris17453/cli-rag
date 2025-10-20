.PHONY: build run clean install test help

# Default target
help:
	@echo "CLI-RAG Makefile"
	@echo ""
	@echo "Targets:"
	@echo "  build        - Build the project"
	@echo "  build-cuda   - Build with CUDA support"
	@echo "  run          - Run the application"
	@echo "  install      - Install dependencies and setup"
	@echo "  publish      - Create release binary"
	@echo "  clean        - Clean build artifacts"
	@echo "  test         - Run tests"
	@echo ""
	@echo "Examples:"
	@echo "  make build"
	@echo "  make run ARGS='ingest https://example.com'"
	@echo "  make run ARGS='query'"

build:
	@echo "Building CLI-RAG..."
	dotnet build -c Release

build-cuda:
	@echo "Building CLI-RAG with CUDA support..."
	dotnet build -c Release -p:EnableCuda=true

run:
	dotnet run -- $(ARGS)

install:
	@echo "Running setup..."
	./setup.sh

publish:
	@echo "Creating release binary..."
	dotnet publish -c Release -r linux-x64 --self-contained -o ./dist
	@echo ""
	@echo "Binary created at: ./dist/CliRag"

publish-cuda:
	@echo "Creating release binary with CUDA..."
	dotnet publish -c Release -r linux-x64 --self-contained -p:EnableCuda=true -o ./dist
	@echo ""
	@echo "Binary created at: ./dist/CliRag"

clean:
	dotnet clean
	rm -rf bin/ obj/ dist/

test:
	dotnet test

# Shortcuts
ingest:
	dotnet run -- ingest $(URL)

query:
	dotnet run -- query

list:
	dotnet run -- list

clear:
	dotnet run -- clear
