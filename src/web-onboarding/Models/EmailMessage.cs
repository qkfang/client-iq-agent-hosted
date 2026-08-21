namespace Onboarding.Web.Models;

/// <summary>
/// A mock email trace entry linked to a customer record.
/// </summary>
public class EmailMessage
{
    public string EmailId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string Direction { get; set; } = "Outbound";
    public DateTimeOffset SentUtc { get; set; } = DateTimeOffset.UtcNow;
    public string Summary { get; set; } = string.Empty;
    public string Status { get; set; } = "Unread";
}
