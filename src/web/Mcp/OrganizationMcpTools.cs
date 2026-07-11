using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Onboarding.Web.Models;
using Onboarding.Web.Services;

namespace Onboarding.Web.Mcp;

/// <summary>
/// MCP tools for reading and updating the organization/entity details linked
/// to a CRM customer record.
/// </summary>
[McpServerToolType]
public class OrganizationMcpTools
{
    private readonly CrmService _crmService;

    public OrganizationMcpTools(CrmService crmService)
    {
        _crmService = crmService;
    }

    [McpServerTool(Name = "list_crm_organizations"), Description("List the organization/entity details for a customer.")]
    public string ListOrganizations([Description("The CRM customer id, e.g. CUST-1001.")] string customerId) =>
        JsonSerializer.Serialize(_crmService.GetOrganizations(customerId));

    [McpServerTool(Name = "update_crm_organization"), Description("Create or update an organization's entity details for a customer.")]
    public string UpdateOrganization(
        [Description("The organization id, e.g. ORG-1001. Leave empty to create a new organization.")] string organizationId,
        [Description("The CRM customer id this organization belongs to.")] string customerId,
        [Description("Organization/entity name.")] string organizationName,
        [Description("Business unit, e.g. Commodities Desk.")] string? businessUnit = null,
        [Description("Parent organization name, if any.")] string? parentOrganization = null,
        [Description("Address line 1.")] string? addressLine1 = null,
        [Description("City.")] string? city = null,
        [Description("State/province.")] string? state = null,
        [Description("Postal code.")] string? postalCode = null,
        [Description("Country.")] string? country = null,
        [Description("Tax identifier.")] string? taxId = null)
    {
        var organization = new Organization
        {
            OrganizationId = string.IsNullOrWhiteSpace(organizationId) ? $"ORG-{Guid.NewGuid():N}" : organizationId,
            CustomerId = customerId,
            OrganizationName = organizationName,
            BusinessUnit = businessUnit ?? string.Empty,
            ParentOrganization = parentOrganization ?? string.Empty,
            AddressLine1 = addressLine1 ?? string.Empty,
            City = city ?? string.Empty,
            State = state ?? string.Empty,
            PostalCode = postalCode ?? string.Empty,
            Country = country ?? string.Empty,
            TaxId = taxId ?? string.Empty
        };

        return JsonSerializer.Serialize(_crmService.UpsertOrganization(organization));
    }
}
