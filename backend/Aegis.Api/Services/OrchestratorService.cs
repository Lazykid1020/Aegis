using Aegis.Api.Models;

namespace Aegis.Api.Services;

/// <summary>
/// The main orchestrator that implements the Supervisor → Confidence → Guardrail → Execute pipeline.
/// This is the "brain" of the Aegis system.
/// </summary>
public class OrchestratorService
{
    private readonly SupervisorAgent _supervisor;
    private readonly GuardrailAgent _guardrail;
    private readonly ConfidenceScorer _confidenceScorer;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OrchestratorService> _logger;

    // Simple in-memory store for pending HITL actions
    private static readonly Dictionary<string, PendingAction> _pendingActions = new();

    // In-memory feedback store for learning loop
    private static readonly List<FeedbackEntry> _feedbackLog = new();

    public OrchestratorService(
        SupervisorAgent supervisor,
        GuardrailAgent guardrail,
        ConfidenceScorer confidenceScorer,
        IHttpClientFactory httpClientFactory,
        ILogger<OrchestratorService> logger)
    {
        _supervisor = supervisor;
        _guardrail = guardrail;
        _confidenceScorer = confidenceScorer;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<ChatResponse> ProcessAsync(ChatRequest request)
    {
        var sessionId = request.SessionId ?? Guid.NewGuid().ToString();

        // Step 1: Supervisor analyzes intent and gathers info
        _logger.LogInformation("[Session {Session}] Step 1: Analyzing intent...", sessionId);
        var supervisorResult = await _supervisor.AnalyzeIntentAsync(sessionId, request.Message);

        if (!supervisorResult.IsReadyForAction)
        {
            _logger.LogInformation("[Session {Session}] Gathering info: {Message}", sessionId, supervisorResult.Message);
            return new ChatResponse
            {
                SessionId = sessionId,
                Message = supervisorResult.Message,
                ActionTaken = "gathering_info"
            };
        }

        var intent = supervisorResult.Intent;

        // Step 2: Experience-based Confidence Scoring
        _logger.LogInformation("[Session {Session}] Step 2: Scoring confidence...", sessionId);
        var confidence = await _confidenceScorer.ScoreAsync(request.Message, intent);

        // Step 3: Guardrail risk assessment
        _logger.LogInformation("[Session {Session}] Step 3: Assessing risk via Guardrail...", sessionId);
        var risk = await _guardrail.AssessRiskAsync(intent, request.Message);

        // Step 4: Route to the appropriate tier
        _logger.LogInformation("[Session {Session}] Confidence: {Score} ({Tier}), Risk: {Risk}",
            sessionId, confidence.FinalScore, confidence.Tier, risk.RiskLevel);

        // If guardrail blocks, override to Tier 3
        if (risk.IsBlocked)
        {
            _logger.LogWarning("[Session {Session}] BLOCKED by Guardrail: {Reason}", sessionId, risk.PolicyViolation);
            var blockedTicketId = await CreateEnrichedTicket(intent, confidence, risk, request.UserId);
            return new ChatResponse
            {
                SessionId = sessionId,
                Message = $"⚠️ This action has been blocked by security policy: {risk.PolicyViolation}. A ticket **{blockedTicketId}** has been created for manual review.",
                Confidence = confidence,
                Risk = risk,
                ActionTaken = "ticket_created",
                TicketId = blockedTicketId
            };
        }

        // Adjust tier based on risk
        var effectiveTier = DetermineEffectiveTier(confidence, risk);

        return effectiveTier switch
        {
            "Tier1_AutoRemediate" => await HandleTier1(sessionId, intent, confidence, risk),
            "Tier2_HumanInTheLoop" => HandleTier2(sessionId, intent, confidence, risk),
            _ => await HandleTier3(sessionId, intent, confidence, risk, request.UserId)
        };
    }

    private string DetermineEffectiveTier(ConfidenceResult confidence, RiskAssessment risk)
    {
        // High confidence but medium/high risk → bump to Tier 2
        if (confidence.Tier == "Tier1_AutoRemediate" && risk.RiskLevel != "Low")
            return "Tier2_HumanInTheLoop";

        return confidence.Tier;
    }

    private async Task<ChatResponse> HandleTier1(string sessionId, string intent, ConfidenceResult confidence, RiskAssessment risk)
    {
        _logger.LogInformation("[Session {Session}] Tier 1: Auto-remediating...", sessionId);
        var result = await _supervisor.ExecuteToolAsync(intent);

        return new ChatResponse
        {
            SessionId = sessionId,
            Message = $"✅ Issue resolved automatically!\n\n{result}",
            Confidence = confidence,
            Risk = risk,
            ActionTaken = "auto_remediated"
        };
    }

    private ChatResponse HandleTier2(string sessionId, string intent, ConfidenceResult confidence, RiskAssessment risk)
    {
        _logger.LogInformation("[Session {Session}] Tier 2: Awaiting human approval...", sessionId);

        // Store the pending action
        _pendingActions[sessionId] = new PendingAction
        {
            Intent = intent,
            SessionId = sessionId,
            CreatedAt = DateTime.UtcNow
        };

        return new ChatResponse
        {
            SessionId = sessionId,
            Message = $"🔒 This action requires manager approval before execution.\n\n**Proposed action:** {intent}\n**Risk Level:** {risk.RiskLevel}\n**Confidence Score:** {confidence.FinalScore}% (Similarity: {confidence.SimilarityScore}, Historical: {confidence.HistoricalSuccessRate}, LLM: {confidence.LlmCertainty})\n\nPlease click Approve or Reject.",
            Confidence = confidence,
            Risk = risk,
            ActionTaken = "awaiting_approval",
            RequiresApproval = true
        };
    }

    private async Task<ChatResponse> HandleTier3(string sessionId, string intent, ConfidenceResult confidence, RiskAssessment risk, string userId)
    {
        _logger.LogInformation("[Session {Session}] Tier 3: Escalating to human agent...", sessionId);

        // Actually create an enriched ticket via the mock ServiceNow API
        var ticketId = await CreateEnrichedTicket(intent, confidence, risk, userId);

        return new ChatResponse
        {
            SessionId = sessionId,
            Message = $"📋 I don't have enough confidence to resolve this automatically. A detailed ticket **{ticketId}** has been created and assigned to the appropriate team.\n\n**Summary:** {intent}\n**Confidence:** {confidence.FinalScore}% (Similarity: {confidence.SimilarityScore}, Historical: {confidence.HistoricalSuccessRate}, LLM: {confidence.LlmCertainty})\n**Risk:** {risk.RiskLevel} — {risk.Reasoning}",
            Confidence = confidence,
            Risk = risk,
            ActionTaken = "ticket_created",
            TicketId = ticketId
        };
    }

    /// <summary>
    /// Creates an enriched, context-rich ticket in the mock ServiceNow system.
    /// Includes confidence breakdown and risk assessment for the human agent.
    /// </summary>
    private async Task<string> CreateEnrichedTicket(string intent, ConfidenceResult confidence, RiskAssessment risk, string userId)
    {
        var ticket = new TicketRecord
        {
            Title = intent,
            Description = $"AI-generated ticket (Aegis)\n\n" +
                          $"Intent: {intent}\n" +
                          $"Confidence Score: {confidence.FinalScore}%\n" +
                          $"  - Similarity: {confidence.SimilarityScore}%\n" +
                          $"  - Historical Success: {confidence.HistoricalSuccessRate}%\n" +
                          $"  - LLM Certainty: {confidence.LlmCertainty}%\n" +
                          $"Risk Level: {risk.RiskLevel} (Score: {risk.RiskScore})\n" +
                          $"Risk Reasoning: {risk.Reasoning}\n" +
                          (risk.PolicyViolation != null ? $"Policy Violation: {risk.PolicyViolation}\n" : ""),
            Category = "IT",
            Urgency = risk.RiskScore >= 80 ? "High" : "Medium",
            ReportedBy = userId
        };

        try
        {
            var client = _httpClientFactory.CreateClient("MockApi");
            var response = await client.PostAsJsonAsync("/api/mock/servicenow/ticket", ticket);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<TicketCreationResponse>();
                _logger.LogInformation("Created enriched ticket: {TicketId}", result?.TicketId);
                return result?.TicketId ?? ticket.Id;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create ticket via mock API, using local ID.");
        }

        return ticket.Id;
    }

    public async Task<ChatResponse> ApproveActionAsync(string sessionId)
    {
        if (!_pendingActions.TryGetValue(sessionId, out var pending))
        {
            return new ChatResponse
            {
                SessionId = sessionId,
                Message = "No pending action found for this session.",
                ActionTaken = "none"
            };
        }

        _pendingActions.Remove(sessionId);
        var result = await _supervisor.ExecuteToolAsync(pending.Intent);

        return new ChatResponse
        {
            SessionId = sessionId,
            Message = $"✅ Action approved and executed!\n\n{result}",
            ActionTaken = "auto_remediated"
        };
    }

    /// <summary>
    /// Records feedback and updates the learning loop.
    /// Negative feedback decreases confidence for similar future issues.
    /// </summary>
    public void RecordFeedback(string sessionId, bool isPositive, string? comment)
    {
        var entry = new FeedbackEntry
        {
            SessionId = sessionId,
            IsPositive = isPositive,
            Comment = comment,
            Timestamp = DateTime.UtcNow
        };

        _feedbackLog.Add(entry);
        _logger.LogInformation("Feedback recorded for {Session}: {Positive} | {Comment}",
            sessionId, isPositive, comment);

        // In production: if negative, re-embed the issue with "escalated" status
        // into the Vector DB, which will lower the HistoricalSuccessRate for similar future issues.
        if (!isPositive)
        {
            _logger.LogWarning("[Learning Loop] Negative feedback for session {Session}. " +
                "In production, this would update the Vector DB to lower confidence for similar issues.", sessionId);
        }
    }
}

public class PendingAction
{
    public string Intent { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class FeedbackEntry
{
    public string SessionId { get; set; } = string.Empty;
    public bool IsPositive { get; set; }
    public string? Comment { get; set; }
    public DateTime Timestamp { get; set; }
}

internal class TicketCreationResponse
{
    public bool Success { get; set; }
    public string? TicketId { get; set; }
    public string? Message { get; set; }
}
