namespace Onboarding.Web.Models;

/// <summary>
/// A mock sales opportunity linked to a customer record.
/// </summary>
public class Opportunity
{
    public string OpportunityId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal EstimatedValue { get; set; }
    public string Currency { get; set; } = "USD";
    public string Stage { get; set; } = "Qualify";
    public int Probability { get; set; }
    public DateTimeOffset EstimatedCloseUtc { get; set; } = DateTimeOffset.UtcNow;
    public string Owner { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
