using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

#pragma warning disable SKEXP0070

namespace Aegis.Api.Services;

/// <summary>
/// MVP Base Agent: understands user intent, retrieves context, and can create a ticket.
/// </summary>
public class BaseAgent
{
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatService;
    private readonly ILogger<BaseAgent> _logger;
    private static readonly Dictionary<string, ChatHistory> _sessions = new();

    public BaseAgent(Kernel kernel, ILogger<BaseAgent> logger)
    {
        _kernel = kernel;
        _chatService = _kernel.GetRequiredService<IChatCompletionService>();
        _logger = logger;
    }

    public async Task<string> ProcessMessageAsync(string sessionId, string message, string contextFromVectorDb)
    {
        var history = GetOrCreateHistory(sessionId, contextFromVectorDb);
        history.AddUserMessage(message);

        var settings = new PromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        try 
        {
            var result = await _chatService.GetChatMessageContentAsync(history, settings, _kernel);
            var responseText = result.Content ?? string.Empty;
            history.AddAssistantMessage(responseText);
            return responseText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message");
            return "Sorry, I encountered an error processing your request.";
        }
    }

    public async Task<(double Score, string Reasoning)> EvaluateConfidenceAsync(string sessionId, string userMessage, string contextFromVectorDb)
    {
        var scyllaHistory = new ChatHistory(@"You are an IT Support Confidence Evaluator. 
Your job is to analyze the conversation history, the provided Knowledge Base Context, and the user's latest message.
Determine your confidence that you can safely and effectively resolve the user's issue right now, WITHOUT needing human IT staff intervention.
IMPORTANT: If the user indicates that your previous solution FAILED or did not work, you must significantly LOWER your confidence.
Respond strictly in JSON format: { ""confidenceScore"": 80, ""reasoning"": ""Your reason here"" }.");

        var history = GetOrCreateHistory(sessionId, contextFromVectorDb);
        foreach (var msg in history)
        {
            // Skip the system prompt to keep it clean
            if (msg.Role == AuthorRole.System) continue;
            
            if (msg.Role == AuthorRole.User) scyllaHistory.AddUserMessage(msg.Content ?? string.Empty);
            if (msg.Role == AuthorRole.Assistant) scyllaHistory.AddAssistantMessage(msg.Content ?? string.Empty);
        }
        
        scyllaHistory.AddUserMessage($"Knowledge Base Context:\n{contextFromVectorDb}\n\nUser's Latest Message: {userMessage}");

        var settings = new PromptExecutionSettings 
        { 
            ExtensionData = new Dictionary<string, object> 
            { 
                { "response_mime_type", "application/json" } 
            } 
        };

        try 
        {
            var result = await _chatService.GetChatMessageContentAsync(scyllaHistory, settings, _kernel);
            var content = result.Content ?? "{}";
            
            // Minor cleanup in case LLM wraps json in markdown
            content = content.Replace("```json", "").Replace("```", "").Trim();

            var json = System.Text.Json.JsonDocument.Parse(content);
            double score = json.RootElement.GetProperty("confidenceScore").GetDouble();
            string reasoning = json.RootElement.GetProperty("reasoning").GetString() ?? "No reasoning provided.";
            
            return (score, reasoning);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to evaluate confidence. Defaulting to 50.");
            return (50.0, "Failed to parse JSON confidence evaluation.");
        }
    }

    private ChatHistory GetOrCreateHistory(string sessionId, string contextFromVectorDb)
    {
        if (!_sessions.TryGetValue(sessionId, out var history))
        {
            var systemPrompt = @"You are Aegis, a helpful enterprise IT support agent.
Use the provided Knowledge Base context to answer user questions. 
If the user's issue requires human intervention or cannot be solved with the given knowledge, use the `create_support_ticket` tool to raise a ticket for them.

Knowledge Base Context:
" + contextFromVectorDb;

            history = new ChatHistory(systemPrompt);
            _sessions[sessionId] = history;
        }
        return history;
    }
}

#pragma warning restore SKEXP0070
