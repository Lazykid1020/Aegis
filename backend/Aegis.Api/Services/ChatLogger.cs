using System.Text.Json;

namespace Aegis.Api.Services;

/// <summary>
/// Simple chat logger that writes conversation turns to JSON Lines files.
/// One file per session in logs/chats/{sessionId}.jsonl
/// </summary>
public class ChatLogger
{
    private readonly ILogger<ChatLogger> _logger;
    private readonly string _logDirectory;

    public ChatLogger(ILogger<ChatLogger> logger)
    {
        _logger = logger;
        _logDirectory = Path.Combine(Directory.GetCurrentDirectory(), "logs", "chats");
        Directory.CreateDirectory(_logDirectory);
    }

    public async Task LogTurnAsync(string sessionId, string userId, string userMessage, string agentResponse, double confidence)
    {
        var entry = new
        {
            timestamp = DateTime.UtcNow.ToString("o"),
            sessionId,
            userId,
            userMessage,
            agentResponse,
            confidence = Math.Round(confidence, 2)
        };

        var json = JsonSerializer.Serialize(entry, new JsonSerializerOptions { WriteIndented = false });
        var filePath = Path.Combine(_logDirectory, $"{sessionId}.jsonl");

        try
        {
            await File.AppendAllTextAsync(filePath, json + Environment.NewLine);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write chat log for session {SessionId}", sessionId);
        }
    }
}
