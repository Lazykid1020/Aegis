namespace Aegis.Api.Models;

public class ChatResponse
{
    public string Message { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string ActionTaken { get; set; } = "none";
    public string? TicketId { get; set; }
    public bool RequiresApproval { get; set; } = false;
}
