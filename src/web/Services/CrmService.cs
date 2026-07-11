using System.Collections.Concurrent;
using System.Text.Json;
using Onboarding.Web.Models;

namespace Onboarding.Web.Services;

/// <summary>
/// In-memory stand-in for the Dynamics 365 CRM. Seeded at startup from the
/// JSON files under the data/ folder, and updated in memory by the web UI
/// and the MCP tools that the Foundry onboarding agent calls.
/// </summary>
public class CrmService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ConcurrentDictionary<string, CrmRecord> _customers = new();
    private readonly ConcurrentDictionary<string, Organization> _organizations = new();
    private readonly ConcurrentDictionary<string, HistoryEntry> _history = new();
    private readonly ConcurrentDictionary<string, EmailMessage> _emails = new();
    private readonly ConcurrentDictionary<string, Opportunity> _opportunities = new();

    public CrmService(IHostEnvironment environment)
    {
        var dataFolder = Path.Combine(environment.ContentRootPath, "data");

        Load(dataFolder, "customers.json", _customers, r => r.CustomerId);
        Load(dataFolder, "organizations.json", _organizations, r => r.OrganizationId);
        Load(dataFolder, "history.json", _history, r => r.HistoryId);
        Load(dataFolder, "emails.json", _emails, r => r.EmailId);
        Load(dataFolder, "opportunities.json", _opportunities, r => r.OpportunityId);
    }

    // Customers
    public IReadOnlyCollection<CrmRecord> GetCustomers() => _customers.Values.OrderBy(r => r.CustomerId).ToList();

    public CrmRecord? GetCustomer(string customerId) => _customers.GetValueOrDefault(customerId);

    public CrmRecord UpsertCustomer(CrmRecord record)
    {
        record.LastUpdatedUtc = DateTimeOffset.UtcNow;
        _customers[record.CustomerId] = record;
        return record;
    }

    // Organizations
    public IReadOnlyCollection<Organization> GetOrganizations(string customerId) =>
        _organizations.Values.Where(o => o.CustomerId == customerId).ToList();

    public Organization UpsertOrganization(Organization organization)
    {
        _organizations[organization.OrganizationId] = organization;
        return organization;
    }

    // History
    public IReadOnlyCollection<HistoryEntry> GetHistory(string customerId) =>
        _history.Values.Where(h => h.CustomerId == customerId).OrderByDescending(h => h.ActivityDate).ToList();

    public HistoryEntry AddHistory(HistoryEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.HistoryId))
        {
            entry.HistoryId = $"HIST-{Guid.NewGuid():N}";
        }
        _history[entry.HistoryId] = entry;
        return entry;
    }

    // Emails
    public IReadOnlyCollection<EmailMessage> GetEmails(string customerId) =>
        _emails.Values.Where(e => e.CustomerId == customerId).OrderByDescending(e => e.SentUtc).ToList();

    public EmailMessage AddEmail(EmailMessage email)
    {
        if (string.IsNullOrWhiteSpace(email.EmailId))
        {
            email.EmailId = $"EMAIL-{Guid.NewGuid():N}";
        }
        _emails[email.EmailId] = email;
        return email;
    }

    // Opportunities
    public IReadOnlyCollection<Opportunity> GetOpportunities(string customerId) =>
        _opportunities.Values.Where(o => o.CustomerId == customerId).ToList();

    public Opportunity UpsertOpportunity(Opportunity opportunity)
    {
        if (string.IsNullOrWhiteSpace(opportunity.OpportunityId))
        {
            opportunity.OpportunityId = $"OPP-{Guid.NewGuid():N}";
        }
        _opportunities[opportunity.OpportunityId] = opportunity;
        return opportunity;
    }

    private static void Load<T>(string dataFolder, string fileName, ConcurrentDictionary<string, T> target, Func<T, string> keySelector)
    {
        var path = Path.Combine(dataFolder, fileName);
        if (!File.Exists(path))
        {
            return;
        }

        var json = File.ReadAllText(path);
        var records = JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? [];
        foreach (var record in records)
        {
            target[keySelector(record)] = record;
        }
    }
}
