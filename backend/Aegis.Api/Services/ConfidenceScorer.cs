using Microsoft.SemanticKernel;
using Aegis.Api.Models;

namespace Aegis.Api.Services;

/// <summary>
/// Experience-based Confidence Scorer.
/// Combines three signals: Vector DB similarity, historical success rate, and LLM certainty.
/// </summary>
public class ConfidenceScorer
{
    private readonly Kernel _kernel;
    private readonly ILogger<ConfidenceScorer> _logger;

    // Simulated historical data (in production, this comes from the Vector DB / Experience Engine)
    private static readonly List<HistoricalIssue> _historicalIssues = new()
    {
        new("password reset", 95, "PasswordResetPlugin", true),
        new("account locked", 92, "AccountUnlockPlugin", true),
        new("vpn not working", 60, "HumanAgent", false),
        new("software installation", 75, "SoftwareInstallPlugin", true),
        new("email not syncing", 70, "HumanAgent", false),
        new("printer not working", 40, "HumanAgent", false),
        new("new laptop request", 30, "TicketEscalation", false),
        new("access request", 80, "AccessManagementPlugin", true),
        new("server down", 20, "HumanAgent", false),
        new("data export", 10, "HumanAgent", false),
    };

    public ConfidenceScorer(Kernel kernel, ILogger<ConfidenceScorer> logger)
    {
        _kernel = kernel;
        _logger = logger;
    }

    public async Task<ConfidenceResult> ScoreAsync(string userMessage, string intent)
    {
        // Signal 1: Similarity Score (mock Vector DB similarity using keyword matching)
        var similarityScore = CalculateSimilarity(userMessage);

        // Signal 2: Historical Success Rate (Experience Engine)
        var historicalRate = CalculateHistoricalSuccess(userMessage);

        // Signal 3: LLM Certainty
        var llmCertainty = await GetLlmCertaintyAsync(intent);

        var confidence = new ConfidenceResult
        {
            SimilarityScore = similarityScore,
            HistoricalSuccessRate = historicalRate,
            LlmCertainty = llmCertainty
        };

        _logger.LogInformation(
            "Confidence breakdown - Similarity: {Sim}, Historical: {Hist}, LLM: {Llm} → Final: {Final} ({Tier})",
            similarityScore, historicalRate, llmCertainty, confidence.FinalScore, confidence.Tier);

        return confidence;
    }

    private int CalculateSimilarity(string message)
    {
        var lowerMsg = message.ToLowerInvariant();
        var bestMatch = _historicalIssues
            .Select(h => new { Issue = h, Score = CalculateKeywordOverlap(lowerMsg, h.Keywords) })
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();

        return bestMatch?.Score ?? 10;
    }

    private int CalculateHistoricalSuccess(string message)
    {
        var lowerMsg = message.ToLowerInvariant();
        var matches = _historicalIssues
            .Where(h => lowerMsg.Contains(h.Keywords) || h.Keywords.Split(' ').Any(k => lowerMsg.Contains(k)))
            .ToList();

        if (!matches.Any()) return 15; // No historical data → low confidence

        var successfullyResolved = matches.Count(m => m.WasAutoResolved);
        var rate = (int)((double)successfullyResolved / matches.Count * 100);
        return rate;
    }

    private async Task<int> GetLlmCertaintyAsync(string intent)
    {
        var prompt = @"On a scale of 0 to 100, how confident are you that you can fully resolve 
the following IT support issue using standard tools (password reset, account unlock, 
software install, ticket creation)?

Issue: {{$input}}

Respond with ONLY a number between 0 and 100, nothing else.";

        try
        {
            var result = await _kernel.InvokePromptAsync(prompt, new() { ["input"] = intent });
            var value = result.GetValue<string>()?.Trim() ?? "50";
            if (int.TryParse(value, out var score))
                return Math.Clamp(score, 0, 100);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get LLM certainty, defaulting to 50.");
        }

        return 50;
    }

    private int CalculateKeywordOverlap(string message, string keywords)
    {
        var keywordList = keywords.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var matchCount = keywordList.Count(k => message.Contains(k));
        return (int)((double)matchCount / keywordList.Length * 100);
    }
}

internal record HistoricalIssue(string Keywords, int OriginalSimilarity, string ResolvedByTool, bool WasAutoResolved);
