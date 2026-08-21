using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Onboarding.Web.Models;
using Onboarding.Web.Services;

namespace Onboarding.Web.Mcp;

/// <summary>
/// MCP tools that let the Foundry onboarding agent read and update the mock
/// Dynamics 365 CRM customer (account) records.
/// </summary>
[McpServerToolType]
public class CustomerMcpTools
{
    private readonly CrmService _crmService;

    public CustomerMcpTools(CrmService crmService)
    {
        _crmService = crmService;
    }

    [McpServerTool(Name = "list_crm_customers"), Description("List all CRM customer records.")]
    public string ListCustomers() => JsonSerializer.Serialize(_crmService.GetCustomers());

    [McpServerTool(Name = "get_crm_customer"), Description("Get the current CRM record for a customer by customerId.")]
    public string GetCustomer([Description("The CRM customer id, e.g. CUST-1001.")] string customerId)
    {
        var record = _crmService.GetCustomer(customerId);
        return record is null
            ? JsonSerializer.Serialize(new { error = $"No CRM record found for {customerId}." })
            : JsonSerializer.Serialize(record);
    }

    [McpServerTool(Name = "update_crm_customer"), Description("Update the onboarding status, KYC risk rating and notes on a customer's CRM record.")]
    public string UpdateCustomer(
        [Description("The CRM customer id, e.g. CUST-1001.")] string customerId,
        [Description("New onboarding status, e.g. In progress, Ready to trade, Escalated.")] string onboardingStatus,
        [Description("KYC/AML risk rating, e.g. Low, Medium, High.")] string? kycRiskRating = null,
        [Description("Free-text notes describing the update.")] string? notes = null)
    {
        var record = _crmService.GetCustomer(customerId) ?? new CrmRecord { CustomerId = customerId, CustomerName = customerId };

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

        _crmService.UpsertCustomer(record);

        return JsonSerializer.Serialize(record);
    }

    [McpServerTool(Name = "finalize_customer_onboarding"), Description("Create the finalized CRM customer record for an onboarding candidate once research is complete.")]
    public string FinalizeOnboarding(
        [Description("The onboarding candidate id, e.g. ONB-001.")] string candidateId,
        [Description("Legal company name.")] string customerName,
        [Description("Legal entity type, e.g. Private Company, Corporation.")] string legalEntityType,
        [Description("Country of registration.")] string country,
        [Description("Primary industry.")] string industry,
        [Description("Primary contact full name.")] string primaryContactName,
        [Description("Primary contact email.")] string primaryContactEmail,
        [Description("Company website.")] string website,
        [Description("KYC/AML risk rating, e.g. Low, Medium, High.")] string kycRiskRating,
        [Description("Final onboarding status, e.g. Ready to trade.")] string onboardingStatus,
        [Description("Research summary and onboarding notes.")] string notes)
    {
        var record = new CrmRecord
        {
            CustomerName = customerName,
            LegalEntityType = legalEntityType,
            Country = country,
            Industry = industry,
            PrimaryContactName = primaryContactName,
            PrimaryContactEmail = primaryContactEmail,
            Website = website,
            KycRiskRating = kycRiskRating,
            OnboardingStatus = onboardingStatus,
            Notes = notes,
            LastUpdatedBy = "SalesCRMOnboarding agent"
        };

        var finalized = _crmService.FinalizeOnboarding(candidateId, record);
        return JsonSerializer.Serialize(finalized);
    }
}
