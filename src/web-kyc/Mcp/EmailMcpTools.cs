using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Onboarding.Web.Models;
using Onboarding.Web.Services;

namespace Onboarding.Web.Mcp;

/// <summary>
/// MCP tools for reading and adding email trace entries for a CRM customer
/// record.
/// </summary>
[McpServerToolType]
public class EmailMcpTools
{
    private readonly CrmService _crmService;

    public EmailMcpTools(CrmService crmService)
    {
        _crmService = crmService;
    }

    [McpServerTool(Name = "list_crm_emails"), Description("List the email trace for a customer, most recent first.")]
    public string ListEmails([Description("The CRM customer id, e.g. CUST-1001.")] string customerId) =>
        JsonSerializer.Serialize(_crmService.GetEmails(customerId));

    [McpServerTool(Name = "add_crm_email"), Description("Add an email trace entry for a customer.")]
    public string AddEmail(
        [Description("The CRM customer id, e.g. CUST-1001.")] string customerId,
        [Description("Email subject.")] string subject,
        [Description("Sender address.")] string from,
        [Description("Recipient address.")] string to,
        [Description("Direction: Inbound or Outbound.")] string direction = "Outbound",
        [Description("Short summary of the email content.")] string? summary = null,
        [Description("Status, e.g. Read, Unread.")] string status = "Unread")
    {
        var email = new EmailMessage
        {
            CustomerId = customerId,
            Subject = subject,
            From = from,
            To = to,
            Direction = direction,
            Summary = summary ?? string.Empty,
            Status = status,
            SentUtc = DateTimeOffset.UtcNow
        };

        return JsonSerializer.Serialize(_crmService.AddEmail(email));
    }
}
