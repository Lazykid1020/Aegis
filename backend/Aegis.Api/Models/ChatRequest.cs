namespace Aegis.Api.Models;

public class ChatRequest
{
    public string UserId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? SessionId { get; set; }
}
