using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json;
using Aegis.Api.Models;

namespace Aegis.Api.Plugins;

/// <summary>
/// IT operations tool. The create_support_ticket function does NOT create
/// a ticket immediately — it stores it as PENDING and returns a message
/// instructing the agent to inform the user. The Orchestrator handles
/// the approval flow.
/// </summary>
public class ITOperationsPlugin
{
    private readonly HttpClient _httpClient;

    // Pending tickets per session — the approval gate
    private static readonly Dictionary<string, TicketPreview> _pendingTickets = new();

    public ITOperationsPlugin(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("MockApi");
    }

    [KernelFunction("create_support_ticket")]
    [Description("Proposes a support ticket for issues that cannot be resolved remotely. The ticket will NOT be created immediately — the user must approve it first. Use this when troubleshooting has been exhausted or the issue clearly needs human intervention.")]
    public Task<string> CreateTicketAsync(
        [Description("Short title summarizing the issue")] string title,
        [Description("Detailed description of the issue and what was tried")] string description,
        [Description("Category: IT, HR, Facilities, or Security")] string category,
        [Description("Urgency level: Low, Medium, High, or Critical")] string urgency)
    {
        // Extract session ID from the call context — fallback to a default
        // The Orchestrator sets this before each agent call
        var sessionId = CurrentSessionId ?? "default";

        var ticket = new TicketPreview
        {
            Action = "REQUEST_TICKET_APPROVAL",
            Title = title,
            Description = description,
            Category = category,
            Urgency = urgency
        };

        _pendingTickets[sessionId] = ticket;

        // Return a message that tells the agent the ticket is pending approval
        return Task.FromResult(
            $"TICKET_PENDING: The ticket has been queued for user approval. " +
            $"Tell the user briefly that you'd like to raise a support ticket for their issue " +
            $"and that they'll see a preview to approve. Do NOT say the ticket has been created yet.");
    }

    /// <summary>
    /// Actually creates the ticket by calling the mock ServiceNow API.
    /// Called by the Orchestrator after user approval.
    /// </summary>
    public async Task<string> ConfirmPendingTicketAsync(string sessionId)
    {
        if (!_pendingTickets.TryGetValue(sessionId, out var ticket))
            return "No pending ticket found for this session.";

        _pendingTickets.Remove(sessionId);

        var response = await _httpClient.PostAsJsonAsync("/api/mock/servicenow/ticket",
            new { title = ticket.Title, description = ticket.Description, category = ticket.Category, urgency = ticket.Urgency, reportedBy = "aegis-ai" });

        if (response.IsSuccessStatusCode)
        {
            return $"Ticket created successfully. Title: {ticket.Title}, Category: {ticket.Category}, Urgency: {ticket.Urgency}";
        }

        return "Failed to create the ticket. Please try again.";
    }

    // --- Static helpers for the Orchestrator ---

    public static bool HasPendingTicket(string sessionId)
        => _pendingTickets.ContainsKey(sessionId);

    public static TicketPreview? GetPendingTicket(string sessionId)
        => _pendingTickets.TryGetValue(sessionId, out var t) ? t : null;

    public static void ClearPendingTicket(string sessionId)
        => _pendingTickets.Remove(sessionId);

    // AsyncLocal session ID flows correctly across async/await boundaries
    private static readonly AsyncLocal<string?> _currentSessionId = new();
    
    public static string? CurrentSessionId
    {
        get => _currentSessionId.Value;
        set => _currentSessionId.Value = value;
    }
}
