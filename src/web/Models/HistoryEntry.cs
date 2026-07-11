namespace Onboarding.Web.Models;

/// <summary>
/// A past activity/history entry (call, meeting, task, note) for a customer.
/// </summary>
public class HistoryEntry
{
    public string HistoryId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string ActivityType { get; set; } = "Note";
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset ActivityDate { get; set; } = DateTimeOffset.UtcNow;
    public string PerformedBy { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
}
