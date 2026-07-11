using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Onboarding.Web.Models;
using Onboarding.Web.Services;

namespace Onboarding.Web.Mcp;

/// <summary>
/// MCP tools that let the Foundry onboarding agent read and update the mock
/// Dynamics 365 CRM records as it works through an onboarding case.
/// </summary>
[McpServerToolType]
public class CrmMcpTools
{
    private readonly CrmService _crmService;

    public CrmMcpTools(CrmService crmService)
    {
        _crmService = crmService;
    }

    [McpServerTool(Name = "get_crm_record"), Description("Get the current CRM record for a customer by customerId.")]
    public string GetCrmRecord([Description("The CRM customer id, e.g. CUST-1001.")] string customerId)
    {
        var record = _crmService.Get(customerId);
        return record is null
            ? JsonSerializer.Serialize(new { error = $"No CRM record found for {customerId}." })
            : JsonSerializer.Serialize(record);
    }

    [McpServerTool(Name = "update_crm_onboarding_status"), Description("Update the onboarding status, KYC risk rating and notes on a customer's CRM record.")]
    public string UpdateCrmOnboardingStatus(
        [Description("The CRM customer id, e.g. CUST-1001.")] string customerId,
        [Description("New onboarding status, e.g. In progress, Ready to trade, Escalated.")] string onboardingStatus,
        [Description("KYC/AML risk rating, e.g. Low, Medium, High.")] string? kycRiskRating = null,
        [Description("Free-text notes describing the update.")] string? notes = null)
    {
        var record = _crmService.Get(customerId) ?? new CrmRecord { CustomerId = customerId, CustomerName = customerId };

        record.OnboardingStatus = onboardingStatus;
        if (!string.IsNullOrWhiteSpace(kycRiskRating))
        {
            record.KycRiskRating = kycRiskRating;
        }
        if (!string.IsNullOrWhiteSpace(notes))
        {
            record.Notes = notes;
        }
        record.LastUpdatedBy = "Foundry onboarding agent";

        _crmService.Upsert(record);

        return JsonSerializer.Serialize(record);
    }
}
