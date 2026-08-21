namespace Onboarding.Web.Models;

/// <summary>
/// A prospective customer queued for onboarding. Basic attributes are captured
/// up front; the SalesCRMOnboarding Foundry agent researches the company and
/// calls back through the /mcp endpoint to finalise a full CRM record.
/// </summary>
public class OnboardingCandidate
{
    public string CandidateId { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string LegalEntityType { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string? CreatedCustomerId { get; set; }
}
