namespace Onboarding.Web.Models;

/// <summary>
/// Mock organization/entity details linked to a CRM customer record.
/// </summary>
public class Organization
{
    public string OrganizationId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string OrganizationName { get; set; } = string.Empty;
    public string ParentOrganization { get; set; } = string.Empty;
    public string BusinessUnit { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
}
