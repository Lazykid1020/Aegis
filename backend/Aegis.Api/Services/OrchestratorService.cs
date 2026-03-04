using Aegis.Api.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Aegis.Api.Services;

/// <summary>
/// Thin orchestrator that owns the confidence score per session.
/// Fetches RAG context, passes the current confidence to the agent,
/// parses delta + ticket signals from the response, and enforces
/// the escalation threshold.
/// </summary>
public class OrchestratorService
{
    private readonly BaseAgent _agent;
    private readonly KnowledgeBaseService _knowledgeBase;
    private readonly ILogger<OrchestratorService> _logger;

    // Server-side confidence tracking per session
    private static readonly Dictionary<string, double> _sessionConfidence = new();
    private static readonly Dictionary<string, TicketPreview> _pendingTickets = new();

    private const double InitialConfidence = 1.0;
    private const double EscalationThreshold = 0.4;

    private static readonly Regex TicketSignalRegex = new(
        @":::TICKET_SIGNAL:::\s*(\{.*?\})\s*:::END_SIGNAL:::",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex ConfidenceDeltaRegex = new(
        @":::CONFIDENCE_DELTA:::\s*(\{.*?\})\s*:::END_DELTA:::",
        RegexOptions.Singleline | RegexOptions.Compiled);

    public OrchestratorService(BaseAgent agent, KnowledgeBaseService knowledgeBase, ILogger<OrchestratorService> logger)
    {
        _agent = agent;
        _knowledgeBase = knowledgeBase;
        _logger = logger;
    }

    public async Task<ChatResponse> ProcessAsync(ChatRequest request)
    {
        var sessionId = request.SessionId ?? Guid.NewGuid().ToString();

        // 1. Get or initialize confidence for this session
        if (!_sessionConfidence.ContainsKey(sessionId))
            _sessionConfidence[sessionId] = InitialConfidence;

        var currentConfidence = _sessionConfidence[sessionId];

        // 2. Fetch RAG context
        var ragContext = await _knowledgeBase.SearchAsync(request.Message);

        // 3. Prepend confidence to the user message so the agent knows the current score
        var enrichedMessage = $"[CONFIDENCE: {currentConfidence:F2}] {request.Message}";

        _logger.LogInformation("[Session {Session}] Confidence: {Confidence:F2} | Sending to agent.", sessionId, currentConfidence);

        // 4. Let the agent handle the conversation
        var rawResponse = await _agent.ProcessMessageAsync(sessionId, enrichedMessage, ragContext);

        // 5. Parse and apply confidence delta (if emitted)
        var deltaMatch = ConfidenceDeltaRegex.Match(rawResponse);
        if (deltaMatch.Success)
        {
            try
            {
                var deltaJson = deltaMatch.Groups[1].Value;
                using var doc = JsonDocument.Parse(deltaJson);
                var delta = doc.RootElement.GetProperty("delta").GetDouble();
                var reason = doc.RootElement.TryGetProperty("reason", out var r) ? r.GetString() : "n/a";

                // Clamp delta to [-1.0, +0.5] to prevent wild swings
                delta = Math.Clamp(delta, -1.0, 0.5);

                var newConfidence = Math.Clamp(currentConfidence + delta, 0.0, 1.0);
                _sessionConfidence[sessionId] = newConfidence;

                _logger.LogInformation("[Session {Session}] Delta: {Delta:+0.00;-0.00} → Confidence: {Old:F2} → {New:F2} | Reason: {Reason}",
                    sessionId, delta, currentConfidence, newConfidence, reason);

                currentConfidence = newConfidence;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Session {Session}] Failed to parse confidence delta.", sessionId);
            }
        }

        // Strip the delta signal from user-facing output
        var cleanResponse = ConfidenceDeltaRegex.Replace(rawResponse, "").Trim();

        // 6. Check if agent already emitted a ticket signal
        var ticketMatch = TicketSignalRegex.Match(cleanResponse);
        if (ticketMatch.Success)
        {
            return HandleTicketSignal(sessionId, cleanResponse, ticketMatch);
        }

        // 7. Check if confidence crossed the threshold — force ticket escalation
        if (currentConfidence < EscalationThreshold && !_pendingTickets.ContainsKey(sessionId))
        {
            _logger.LogInformation("[Session {Session}] Confidence {Confidence:F2} below threshold {Threshold}. Forcing ticket escalation.",
                sessionId, currentConfidence, EscalationThreshold);

            var escalationInstruction =
                "Confidence is critically low. The system requires you to propose a support ticket now. " +
                "Summarize the issue and what was discussed, then output a :::TICKET_SIGNAL::: block. " +
                "Keep your message to the user brief — just explain that this needs human attention.";

            var escalationResponse = await _agent.SendSystemInstructionAsync(sessionId, escalationInstruction, ragContext);

            var escalationClean = ConfidenceDeltaRegex.Replace(escalationResponse, "").Trim();
            var escalationTicket = TicketSignalRegex.Match(escalationClean);

            if (escalationTicket.Success)
            {
                return HandleTicketSignal(sessionId, escalationClean, escalationTicket);
            }

            // Agent didn't emit a signal even when forced — return the response anyway
            return new ChatResponse
            {
                SessionId = sessionId,
                Message = escalationClean,
                ActionTaken = "none"
            };
        }

        // 8. Normal response
        return new ChatResponse
        {
            SessionId = sessionId,
            Message = cleanResponse,
            ActionTaken = "none"
        };
    }

    public async Task<ChatResponse> ApproveActionAsync(string sessionId)
    {
        _logger.LogInformation("[Session {Session}] Ticket approved.", sessionId);

        if (_pendingTickets.TryGetValue(sessionId, out var ticket))
        {
            _pendingTickets.Remove(sessionId);

            // Reset confidence after ticket creation
            _sessionConfidence[sessionId] = InitialConfidence;

            var instruction = $"The user has APPROVED creating the support ticket. " +
                              $"Please now use the create_support_ticket tool with these details: " +
                              $"Title: '{ticket.Title}', Description: '{ticket.Description}', " +
                              $"Category: '{ticket.Category}', Urgency: '{ticket.Urgency}'.";

            var ragContext = await _knowledgeBase.SearchAsync(ticket.Title ?? "");
            var responseText = await _agent.SendSystemInstructionAsync(sessionId, instruction, ragContext);

            // Strip any delta signals from post-approval response
            responseText = ConfidenceDeltaRegex.Replace(responseText, "").Trim();

            return new ChatResponse
            {
                SessionId = sessionId,
                Message = responseText,
                ActionTaken = "ticket_created"
            };
        }

        return new ChatResponse
        {
            SessionId = sessionId,
            Message = "No pending ticket request found for this session.",
            ActionTaken = "none"
        };
    }

    public async Task<ChatResponse> RejectActionAsync(string sessionId)
    {
        _logger.LogInformation("[Session {Session}] Ticket rejected.", sessionId);

        if (_pendingTickets.ContainsKey(sessionId))
            _pendingTickets.Remove(sessionId);

        // Bump confidence slightly after rejection so agent doesn't immediately re-propose
        if (_sessionConfidence.ContainsKey(sessionId))
            _sessionConfidence[sessionId] = Math.Min(_sessionConfidence[sessionId] + 0.15, 1.0);

        var ragContext = await _knowledgeBase.SearchAsync("");
        var responseText = await _agent.SendSystemInstructionAsync(
            sessionId,
            "The user has REJECTED the ticket creation. Acknowledge this briefly and ask if there's anything else you can help with.",
            ragContext);

        responseText = ConfidenceDeltaRegex.Replace(responseText, "").Trim();

        return new ChatResponse
        {
            SessionId = sessionId,
            Message = responseText,
            ActionTaken = "none"
        };
    }

    private ChatResponse HandleTicketSignal(string sessionId, string cleanResponse, Match ticketMatch)
    {
        try
        {
            var json = ticketMatch.Groups[1].Value;
            var ticket = JsonSerializer.Deserialize<TicketPreview>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (ticket != null && ticket.Action == "REQUEST_TICKET_APPROVAL")
            {
                _pendingTickets[sessionId] = ticket;
                var cleanMessage = TicketSignalRegex.Replace(cleanResponse, "").Trim();

                _logger.LogInformation("[Session {Session}] Ticket proposed: {Title}", sessionId, ticket.Title);

                return new ChatResponse
                {
                    SessionId = sessionId,
                    Message = cleanMessage,
                    ActionTaken = "awaiting_approval",
                    RequiresApproval = true,
                    ProposedTicket = ticket
                };
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "[Session {Session}] Failed to parse ticket signal.", sessionId);
        }

        return new ChatResponse
        {
            SessionId = sessionId,
            Message = TicketSignalRegex.Replace(cleanResponse, "").Trim(),
            ActionTaken = "none"
        };
    }
}
