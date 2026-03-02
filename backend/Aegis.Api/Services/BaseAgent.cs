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
        var scyllaHistory = new ChatHistory(@"You are an IT Support AI Confidence Evaluator. 
Analyze the conversation history, the Knowledge Base Context, and the user's latest message.
Determine your confidence (0-100) that you can safely and effectively progress or resolve the user's issue WITHOUT needing human IT staff intervention.

CRITICAL RULES:
1. GREETINGS & CLARIFICATIONS: If the user is just greeting (e.g., 'hey'), asking general questions, or if you need to ask clarifying questions to understand the issue, your confidence MUST BE HIGH (100). Gathering information is normal.
2. TROUBLESHOOTING: If the user states an issue and you have a potential solution to offer based on the context, your confidence is HIGH (80-100).
3. NEGATIVE FEEDBACK: If the user indicates that your previous solution FAILED, you must LOWER your confidence based on how effective you think your remaining solutions are.
4. DEAD END: Only drop your confidence LOW (< 75) if you have exhausted your knowledge base, the issue explicitly requires human intervention, or the user rejected your final troubleshooting step and you have no more ideas.

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
