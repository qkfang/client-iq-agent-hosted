using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Onboarding.Web.Models;
using Onboarding.Web.Services;

namespace Onboarding.Web.Mcp;

/// <summary>
/// MCP tools for reading and updating sales opportunities linked to a CRM
/// customer record.
/// </summary>
[McpServerToolType]
public class OpportunityMcpTools
{
    private readonly CrmService _crmService;

    public OpportunityMcpTools(CrmService crmService)
    {
        _crmService = crmService;
    }

    [McpServerTool(Name = "list_crm_opportunities"), Description("List the sales opportunities for a customer.")]
    public string ListOpportunities([Description("The CRM customer id, e.g. CUST-1001.")] string customerId) =>
        JsonSerializer.Serialize(_crmService.GetOpportunities(customerId));

    [McpServerTool(Name = "update_crm_opportunity"), Description("Create or update a sales opportunity for a customer.")]
    public string UpdateOpportunity(
        [Description("The opportunity id, e.g. OPP-1001-01. Leave empty to create a new opportunity.")] string opportunityId,
        [Description("The CRM customer id this opportunity belongs to.")] string customerId,
        [Description("Opportunity name.")] string name,
        [Description("Estimated value.")] decimal estimatedValue,
        [Description("Currency code, e.g. USD, AUD.")] string currency = "USD",
        [Description("Sales stage, e.g. Qualify, Develop, Propose, Close.")] string stage = "Qualify",
        [Description("Probability percentage, 0-100.")] int probability = 0,
        [Description("Owner of the opportunity.")] string? owner = null,
        [Description("Description of the opportunity.")] string? description = null)
    {
        var opportunity = new Opportunity
        {
            OpportunityId = string.IsNullOrWhiteSpace(opportunityId) ? $"OPP-{Guid.NewGuid():N}" : opportunityId,
            CustomerId = customerId,
            Name = name,
            EstimatedValue = estimatedValue,
            Currency = currency,
            Stage = stage,
            Probability = probability,
            Owner = owner ?? string.Empty,
            Description = description ?? string.Empty
        };

        return JsonSerializer.Serialize(_crmService.UpsertOpportunity(opportunity));
    }
}
