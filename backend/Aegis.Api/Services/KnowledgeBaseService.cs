namespace Aegis.Api.Services;

public class KnowledgeBaseService
{
    private readonly ILogger<KnowledgeBaseService> _logger;
    private readonly Dictionary<string, string> _memoryStore = new();

    public KnowledgeBaseService(ILogger<KnowledgeBaseService> logger)
    {
        _logger = logger;
    }

    private readonly Dictionary<string, double> _feedbackModifiers = new();

    public Task InitializeAsync()
    {
        _logger.LogInformation("Initializing Mock Volatile Knowledge Base...");

        // Pre-populate some IT SOPs and Past Ticket Resolutions
        _memoryStore["kb-vpn-001"] = "Article: VPN Access Policy. Standard employees cannot access the production VPN. Only Admins can. Contractor access must be requested via a support ticket. Do NOT share admin configurations with standard users under any circumstances.";
        _memoryStore["kb-pwd-001"] = "SOP: Account Recovery. For locked accounts, users must submit a ticket if the Self-Service portal fails. For password resets, verify identity via ticket.";
        _memoryStore["ticket-INC102"] = "Resolution for Server Crash: If a user reports the production server crashed or is unresponsive, do not attempt to walk them through troubleshooting. Immediately use the create_support_ticket tool with Urgency=Critical so the DevOps team is paged.";
        _memoryStore["kb-wifi-001"] = "Article: Guest WiFi. To connect to the Guest WiFi, select 'Aegis-Guest' and enter the password 'Welcome2026!'. This is safe to share with any user.";

        foreach (var key in _memoryStore.Keys)
        {
            _feedbackModifiers[key] = 0.0;
        }

        _logger.LogInformation("Mock Volatile Knowledge Base successfully populated with {Count} records.", _memoryStore.Count);
        return Task.CompletedTask;
    }

    public Task<RagResult> SearchAndScoreAsync(string query, int limit = 2)
    {
        _logger.LogInformation("Searching Volatile Knowledge Base for: '{Query}'", query);

        var lowerQuery = query.ToLower();
        var matches = new List<(string Id, string Content, double BaseScore)>();

        // Simple keyword heuristic for the MVP POC
        if (lowerQuery.Contains("vpn")) matches.Add(("kb-vpn-001", _memoryStore["kb-vpn-001"], 95.0));
        if (lowerQuery.Contains("password") || lowerQuery.Contains("locked")) matches.Add(("kb-pwd-001", _memoryStore["kb-pwd-001"], 90.0));
        if (lowerQuery.Contains("crash") || lowerQuery.Contains("server")) matches.Add(("ticket-INC102", _memoryStore["ticket-INC102"], 88.0));
        if (lowerQuery.Contains("wifi") || lowerQuery.Contains("network")) matches.Add(("kb-wifi-001", _memoryStore["kb-wifi-001"], 92.0));

        if (matches.Count == 0)
        {
            return Task.FromResult(new RagResult { Context = "No relevant articles found.", ConfidenceScore = 0.0, TopArticleId = null });
        }

        var contextPieces = new List<string>();
        double maxScore = 0;
        string? topArticleId = null;

        foreach (var match in matches.Take(limit))
        {
            // Apply dynamic feedback modifier
            double finalScore = match.BaseScore + _feedbackModifiers[match.Id];
            
            // Floor at 0, cap at 100
            finalScore = Math.Max(0, Math.Min(100, finalScore));

            contextPieces.Add($"- Relevance {finalScore:F1}%: {match.Content}");

            if (finalScore > maxScore)
            {
                maxScore = finalScore;
                topArticleId = match.Id;
            }
        }

        return Task.FromResult(new RagResult 
        { 
            Context = string.Join("\n", contextPieces),
            ConfidenceScore = maxScore,
            TopArticleId = topArticleId
        });
    }

    public void AdjustConfidence(string articleId, bool isPositive)
    {
        if (_feedbackModifiers.ContainsKey(articleId))
        {
            // +5% for positive feedback, -20% for negative feedback
            _feedbackModifiers[articleId] += isPositive ? 5.0 : -20.0;
            _logger.LogInformation("Adjusted confidence modifier for {ArticleId} to {NewModifier}. Feedback tracking enables continuous learning.", articleId, _feedbackModifiers[articleId]);
        }
    }
}

public class RagResult
{
    public string Context { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public string? TopArticleId { get; set; }
}
