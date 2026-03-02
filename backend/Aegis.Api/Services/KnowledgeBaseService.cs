namespace Aegis.Api.Services;

public class KnowledgeBaseService
{
    private readonly ILogger<KnowledgeBaseService> _logger;
    private readonly Dictionary<string, string> _memoryStore = new();

    public KnowledgeBaseService(ILogger<KnowledgeBaseService> logger)
    {
        _logger = logger;
    }

    public Task InitializeAsync()
    {
        _logger.LogInformation("Initializing Mock Volatile Knowledge Base...");

        // Pre-populate some IT SOPs and Past Ticket Resolutions
        _memoryStore["kb-vpn-001"] = "Article: VPN Access Policy. Standard employees cannot access the production VPN. Only Admins can. Contractor access must be requested via a support ticket. Do NOT share admin configurations with standard users under any circumstances.";
        _memoryStore["kb-pwd-001"] = "SOP: Account Recovery. For locked accounts, users must submit a ticket if the Self-Service portal fails. For password resets, verify identity via ticket.";
        _memoryStore["ticket-INC102"] = "Resolution for Server Crash: If a user reports the production server crashed or is unresponsive, do not attempt to walk them through troubleshooting. Immediately use the create_support_ticket tool with Urgency=Critical so the DevOps team is paged.";
        _memoryStore["kb-wifi-001"] = "Article: Guest WiFi. To connect to the Guest WiFi, select 'Aegis-Guest' and enter the password 'Welcome2026!'. This is safe to share with any user.";

        _logger.LogInformation("Mock Volatile Knowledge Base successfully populated with {Count} records.", _memoryStore.Count);
        return Task.CompletedTask;
    }

    public Task<string> SearchAsync(string query, int limit = 2)
    {
        _logger.LogInformation("Searching Volatile Knowledge Base for: '{Query}'", query);

        var contextPieces = new List<string>();
        var lowerQuery = query.ToLower();

        // Simple keyword heuristic for the MVP POC
        if (lowerQuery.Contains("vpn")) contextPieces.Add($"- Relevance 0.95: {_memoryStore["kb-vpn-001"]}");
        if (lowerQuery.Contains("password") || lowerQuery.Contains("locked")) contextPieces.Add($"- Relevance 0.90: {_memoryStore["kb-pwd-001"]}");
        if (lowerQuery.Contains("crash") || lowerQuery.Contains("server")) contextPieces.Add($"- Relevance 0.88: {_memoryStore["ticket-INC102"]}");
        if (lowerQuery.Contains("wifi") || lowerQuery.Contains("network")) contextPieces.Add($"- Relevance 0.92: {_memoryStore["kb-wifi-001"]}");

        if (contextPieces.Count == 0)
        {
            return Task.FromResult("No relevant Knowledge Base articles found.");
        }

        return Task.FromResult(string.Join("\n", contextPieces.Take(limit)));
    }
}
