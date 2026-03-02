using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace Aegis.Api.Plugins;

/// <summary>
/// Semantic Kernel Plugin for IT operations: password reset, account unlock.
/// These functions are automatically discovered by SK's auto function-calling.
/// </summary>
public class ITOperationsPlugin
{
    private readonly HttpClient _httpClient;

    public ITOperationsPlugin(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("MockApi");
    }

    [KernelFunction("reset_password")]
    [Description("Resets the password for a user account. Use this when a user reports they forgot their password or need a password reset.")]
    public async Task<string> ResetPasswordAsync(
        [Description("The username of the user whose password needs to be reset")] string username)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/mock/activedirectory/reset-password",
            new { username, reason = "User requested password reset via Aegis AI" });

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadAsStringAsync();
            return $"Password has been successfully reset for user '{username}'. A temporary password has been generated and will be shared securely.";
        }

        return $"Failed to reset password for '{username}'. Please try again or contact IT support.";
    }

    [KernelFunction("unlock_account")]
    [Description("Unlocks a locked user account. Use this when a user reports their account is locked or they cannot log in due to too many failed attempts.")]
    public async Task<string> UnlockAccountAsync(
        [Description("The username of the locked account")] string username)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/mock/activedirectory/unlock",
            new { username, reason = "Account unlock via Aegis AI" });

        if (response.IsSuccessStatusCode)
        {
            return $"Account for user '{username}' has been successfully unlocked. They should be able to log in now.";
        }

        return $"Failed to unlock account for '{username}'. Escalating to IT support.";
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

    [KernelFunction("lookup_employee")]
    [Description("Looks up employee details from the HR system. Use this to verify a user's identity, role, or department.")]
    public async Task<string> LookupEmployeeAsync(
        [Description("The employee ID to look up")] string employeeId)
    {
        var response = await _httpClient.GetAsync($"/api/mock/hr/employee/{employeeId}");

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadAsStringAsync();
            return $"Employee found: {result}";
        }

        return $"Employee with ID '{employeeId}' not found in the system.";
    }
}
