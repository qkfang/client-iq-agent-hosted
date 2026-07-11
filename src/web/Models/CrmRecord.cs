namespace Onboarding.Web.Models;

/// <summary>
/// A mock Dynamics 365 CRM customer record used to demonstrate the onboarding
/// workflow. All data is illustrative and safe to display.
/// </summary>
public class CrmRecord
{
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string LegalEntityType { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string OnboardingStatus { get; set; } = "Pending";
    public string KycRiskRating { get; set; } = "Not assessed";
    public string LastUpdatedBy { get; set; } = "System";
    public DateTimeOffset LastUpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public string Notes { get; set; } = string.Empty;
}
