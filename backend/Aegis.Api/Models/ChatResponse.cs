namespace Aegis.Api.Models;

public class ChatResponse
{
    public string Message { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string ActionTaken { get; set; } = "none";
    public string? TicketId { get; set; }
    public bool RequiresApproval { get; set; } = false;

    /// <summary>
    /// When the agent proposes a ticket, this contains the details for the user to review.
    /// </summary>
    public TicketPreview? ProposedTicket { get; set; }
}

/// <summary>
/// Represents the ticket details the agent proposes before the user approves.
/// </summary>
public class TicketPreview
{
    public string Action { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "IT";
    public string Urgency { get; set; } = "Medium";
}
