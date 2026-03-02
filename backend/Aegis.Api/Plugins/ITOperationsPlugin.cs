using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Net.Http.Json;

namespace Aegis.Api.Plugins;

public class ITOperationsPlugin
{
    private readonly HttpClient _httpClient;

    public ITOperationsPlugin(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("MockApi");
    }

    [KernelFunction("create_support_ticket")]
    [Description("Creates a ServiceNow IT support ticket for issues that cannot be auto-resolved. Use this for complex issues, hardware problems, or when other tools cannot handle the request.")]
    public async Task<string> CreateTicketAsync(
        [Description("Short title summarizing the issue")] string title,
        [Description("Detailed description of the issue")] string description,
        [Description("Category: IT, HR, Facilities, or Security")] string category,
        [Description("Urgency level: Low, Medium, High, or Critical")] string urgency)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/mock/servicenow/ticket",
            new { title, description, category, urgency, reportedBy = "aegis-ai" });

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadAsStringAsync();
            return $"Support ticket created successfully.\nTitle: {title}\nCategory: {category}\nUrgency: {urgency}\nA support agent will follow up shortly.";
        }

        return "Failed to create support ticket. Please try again.";
    }
}
