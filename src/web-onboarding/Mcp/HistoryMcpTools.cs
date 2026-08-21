using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Onboarding.Web.Models;
using Onboarding.Web.Services;

namespace Onboarding.Web.Mcp;

/// <summary>
/// MCP tools for reading and adding past activity/history entries for a
/// CRM customer record.
/// </summary>
[McpServerToolType]
public class HistoryMcpTools
{
    private readonly CrmService _crmService;

    public HistoryMcpTools(CrmService crmService)
    {
        _crmService = crmService;
    }

    [McpServerTool(Name = "list_crm_history"), Description("List past activity/history entries for a customer, most recent first.")]
    public string ListHistory([Description("The CRM customer id, e.g. CUST-1001.")] string customerId) =>
        JsonSerializer.Serialize(_crmService.GetHistory(customerId));

    [McpServerTool(Name = "add_crm_history_entry"), Description("Add a new activity/history entry (call, meeting, task or note) for a customer.")]
    public string AddHistoryEntry(
        [Description("The CRM customer id, e.g. CUST-1001.")] string customerId,
        [Description("Activity type, e.g. Call, Meeting, Task, Note.")] string activityType,
        [Description("Short subject line.")] string subject,
        [Description("Description of the activity.")] string? description = null,
        [Description("Who performed the activity.")] string? performedBy = null,
        [Description("Outcome of the activity.")] string? outcome = null)
    {
        var entry = new HistoryEntry
        {
            CustomerId = customerId,
            ActivityType = activityType,
            Subject = subject,
            Description = description ?? string.Empty,
            PerformedBy = performedBy ?? "Foundry onboarding agent",
            Outcome = outcome ?? string.Empty,
            ActivityDate = DateTimeOffset.UtcNow
        };

        return JsonSerializer.Serialize(_crmService.AddHistory(entry));
    }
}
