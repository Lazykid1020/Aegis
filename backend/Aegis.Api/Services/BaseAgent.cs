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
}

#pragma warning restore SKEXP0070
