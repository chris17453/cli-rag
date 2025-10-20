using CliRag.Config;
using CliRag.LLM;
using CliRag.VectorStore;
using Spectre.Console;

namespace CliRag.Agent;

/// <summary>
/// True Agentic RAG with:
/// - LLM-driven query refinement
/// - Multi-step retrieval
/// - Self-reflection
/// - Chain of thought reasoning
/// </summary>
public class TrueAgenticRAG
{
    private readonly LocalLLM _llm;
    private readonly VectorDb _vectorDb;
    private readonly AppConfig _config;

    public TrueAgenticRAG(LocalLLM llm, VectorDb vectorDb, AppConfig config)
    {
        _llm = llm;
        _vectorDb = vectorDb;
        _config = config;
    }

    public async Task<AgenticResponse> QueryAsync(string userQuery, bool showReasoning = false)
    {
        if (!_llm.IsAvailable)
        {
            return new AgenticResponse
            {
                Answer = "Error: LLM is not available. Please configure a model.",
                Sources = new List<SourceCitation>(),
                ReasoningSteps = new List<string> { "LLM not available" }
            };
        }

        var reasoningSteps = new List<string>();

        try
        {
            // Step 1: Analyze the query and plan retrieval strategy
            if (showReasoning) AnsiConsole.MarkupLine("[cyan]Step 1: Analyzing query and planning...[/]");

            var plan = await PlanRetrievalAsync(userQuery);
            reasoningSteps.Add($"Query Analysis: {plan.Analysis}");
            reasoningSteps.Add($"Planned searches: {string.Join(", ", plan.SearchQueries)}");

            // Step 2: Execute multi-hop retrieval
            if (showReasoning) AnsiConsole.MarkupLine("[cyan]Step 2: Multi-hop retrieval...[/]");

            var allResults = new List<SearchResult>();
            foreach (var searchQuery in plan.SearchQueries)
            {
                var results = await _vectorDb.SearchAsync(searchQuery, _config.TopK);
                allResults.AddRange(results);
                reasoningSteps.Add($"Retrieved {results.Count} results for: '{searchQuery}'");
            }

            // Deduplicate
            var uniqueResults = allResults
                .GroupBy(r => r.Text)
                .Select(g => g.OrderByDescending(r => r.Score).First())
                .OrderByDescending(r => r.Score)
                .Take(_config.TopK * 2)
                .ToList();

            if (uniqueResults.Count == 0)
            {
                return new AgenticResponse
                {
                    Answer = "I don't have enough information to answer this question. Please ingest relevant documents first.",
                    Sources = new List<SourceCitation>(),
                    ReasoningSteps = reasoningSteps
                };
            }

            // Step 3: Generate initial answer
            if (showReasoning) AnsiConsole.MarkupLine("[cyan]Step 3: Generating initial answer...[/]");

            var initialAnswer = await GenerateAnswerAsync(userQuery, uniqueResults);
            reasoningSteps.Add($"Generated initial answer ({initialAnswer.Length} chars)");

            // Step 4: Self-reflection - is the answer good enough?
            if (showReasoning) AnsiConsole.MarkupLine("[cyan]Step 4: Self-reflection...[/]");

            var reflection = await ReflectOnAnswerAsync(userQuery, initialAnswer, uniqueResults);
            reasoningSteps.Add($"Reflection: {reflection.Assessment}");

            string finalAnswer = initialAnswer;

            // Step 5: Refine if needed
            if (reflection.NeedsRefinement)
            {
                if (showReasoning) AnsiConsole.MarkupLine("[cyan]Step 5: Refining answer...[/]");

                reasoningSteps.Add($"Refinement needed: {reflection.Reason}");

                // Try additional retrieval if suggested
                if (reflection.SuggestedQuery != null)
                {
                    var additionalResults = await _vectorDb.SearchAsync(reflection.SuggestedQuery, _config.TopK);
                    uniqueResults.AddRange(additionalResults);
                    reasoningSteps.Add($"Additional retrieval with: '{reflection.SuggestedQuery}' ({additionalResults.Count} results)");
                }

                // Regenerate answer with all context
                finalAnswer = await GenerateAnswerAsync(userQuery, uniqueResults);
                reasoningSteps.Add("Generated refined answer");
            }
            else
            {
                if (showReasoning) AnsiConsole.MarkupLine("[cyan]Step 5: Answer validated ✓[/]");
                reasoningSteps.Add("Answer validated - no refinement needed");
            }

            // Prepare sources
            var sources = uniqueResults.Take(_config.TopK).Select((r, i) => new SourceCitation
            {
                Index = i + 1,
                Url = r.Url,
                Title = r.Title,
                Text = r.Text,
                Score = r.Score
            }).ToList();

            return new AgenticResponse
            {
                Answer = finalAnswer,
                Sources = sources,
                ReasoningSteps = reasoningSteps
            };
        }
        catch (Exception ex)
        {
            reasoningSteps.Add($"Error: {ex.Message}");
            return new AgenticResponse
            {
                Answer = $"Error during agentic processing: {ex.Message}",
                Sources = new List<SourceCitation>(),
                ReasoningSteps = reasoningSteps
            };
        }
    }

    private async Task<RetrievalPlan> PlanRetrievalAsync(string query)
    {
        var prompt = $@"<|start_header_id|>system<|end_header_id|>

You are a query planner. Analyze the user's question and create 2-3 search queries that will help find relevant information.

Output format:
ANALYSIS: [brief analysis of what information is needed]
QUERIES: [query1] | [query2] | [query3]<|eot_id|><|start_header_id|>user<|end_header_id|>

Question: {query}<|eot_id|><|start_header_id|>assistant<|end_header_id|>

ANALYSIS:";

        var response = await _llm.GenerateAsync(prompt);

        // Parse response
        var lines = response.Split('\n');
        var analysis = "";
        var queries = new List<string> { query }; // Always include original

        foreach (var line in lines)
        {
            if (line.StartsWith("ANALYSIS:", StringComparison.OrdinalIgnoreCase))
            {
                analysis = line.Substring(9).Trim();
            }
            else if (line.StartsWith("QUERIES:", StringComparison.OrdinalIgnoreCase))
            {
                var queryPart = line.Substring(8).Trim();
                var splitQueries = queryPart.Split('|').Select(q => q.Trim()).Where(q => !string.IsNullOrEmpty(q));
                queries.AddRange(splitQueries);
            }
        }

        return new RetrievalPlan
        {
            Analysis = string.IsNullOrEmpty(analysis) ? "Analyzing query..." : analysis,
            SearchQueries = queries.Distinct().Take(3).ToList()
        };
    }

    private async Task<string> GenerateAnswerAsync(string query, List<SearchResult> context)
    {
        var topContext = context.Take(3).ToList();
        var contextText = string.Join("\n\n", topContext.Select(r => r.Text));

        var prompt = $@"<|start_header_id|>system<|end_header_id|>

Answer questions based on the provided information.<|eot_id|><|start_header_id|>user<|end_header_id|>

Context:
{contextText}

Based on the above information, {query}<|eot_id|><|start_header_id|>assistant<|end_header_id|>

ANSWER:";

        var response = await _llm.GenerateAsync(prompt);

        if (response.Contains("ANSWER:", StringComparison.OrdinalIgnoreCase))
        {
            var answerStart = response.IndexOf("ANSWER:", StringComparison.OrdinalIgnoreCase) + 7;
            response = response.Substring(answerStart).Trim();
        }

        return response;
    }

    private async Task<ReflectionResult> ReflectOnAnswerAsync(string query, string answer, List<SearchResult> context)
    {
        var prompt = $@"<|start_header_id|>system<|end_header_id|>

Evaluate if this answer adequately addresses the question. Be critical.

Output format:
ASSESSMENT: [good/needs_refinement]
REASON: [why it's good or what's missing]
SUGGESTED_QUERY: [optional - a new search query if more info is needed]<|eot_id|><|start_header_id|>user<|end_header_id|>

Question: {query}

Answer: {answer.Substring(0, Math.Min(500, answer.Length))}

Evaluate this answer:<|eot_id|><|start_header_id|>assistant<|end_header_id|>

ASSESSMENT:";

        var response = await _llm.GenerateAsync(prompt);

        // Parse reflection
        var needsRefinement = response.Contains("needs_refinement", StringComparison.OrdinalIgnoreCase);
        var reason = "";
        string? suggestedQuery = null;

        var lines = response.Split('\n');
        foreach (var line in lines)
        {
            if (line.StartsWith("REASON:", StringComparison.OrdinalIgnoreCase))
            {
                reason = line.Substring(7).Trim();
            }
            else if (line.StartsWith("SUGGESTED_QUERY:", StringComparison.OrdinalIgnoreCase))
            {
                suggestedQuery = line.Substring(16).Trim();
            }
        }

        return new ReflectionResult
        {
            NeedsRefinement = needsRefinement,
            Assessment = needsRefinement ? "Needs refinement" : "Answer is adequate",
            Reason = string.IsNullOrEmpty(reason) ? "Answer quality check complete" : reason,
            SuggestedQuery = suggestedQuery
        };
    }
}

public class RetrievalPlan
{
    public required string Analysis { get; set; }
    public required List<string> SearchQueries { get; set; }
}

public class ReflectionResult
{
    public bool NeedsRefinement { get; set; }
    public required string Assessment { get; set; }
    public required string Reason { get; set; }
    public string? SuggestedQuery { get; set; }
}
