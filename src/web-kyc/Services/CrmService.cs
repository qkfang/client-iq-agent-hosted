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
    private readonly ConcurrentDictionary<string, OnboardingCandidate> _candidates = new();
    private int _customerSeq = 2000;

    public CrmService(IHostEnvironment environment)
    {
        var dataFolder = Path.Combine(environment.ContentRootPath, "data");

        Load(dataFolder, "customers.json", _customers, r => r.CustomerId);
        Load(dataFolder, "organizations.json", _organizations, r => r.OrganizationId);
        Load(dataFolder, "history.json", _history, r => r.HistoryId);
        Load(dataFolder, "emails.json", _emails, r => r.EmailId);
        Load(dataFolder, "opportunities.json", _opportunities, r => r.OpportunityId);

        foreach (var candidate in SeedCandidates())
        {
            _candidates[candidate.CandidateId] = candidate;
        }
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

    public bool DeleteCustomer(string customerId) => _customers.TryRemove(customerId, out _);

    // Onboarding candidates
    public IReadOnlyCollection<OnboardingCandidate> GetOnboardingCandidates() =>
        _candidates.Values.OrderBy(c => c.CandidateId).ToList();

    public OnboardingCandidate? GetOnboardingCandidate(string candidateId) =>
        _candidates.GetValueOrDefault(candidateId);

    public void SetCandidateStatus(string candidateId, string status)
    {
        if (_candidates.TryGetValue(candidateId, out var candidate))
        {
            candidate.Status = status;
        }
    }

    /// <summary>
    /// Creates the CRM record produced by the onboarding agent and marks the
    /// originating candidate as onboarded.
    /// </summary>
    public CrmRecord FinalizeOnboarding(string candidateId, CrmRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.CustomerId))
        {
            record.CustomerId = $"CUST-{Interlocked.Increment(ref _customerSeq)}";
        }
        UpsertCustomer(record);

        if (_candidates.TryGetValue(candidateId, out var candidate))
        {
            candidate.Status = "Onboarded";
            candidate.CreatedCustomerId = record.CustomerId;
        }

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

    private static IEnumerable<OnboardingCandidate> SeedCandidates() =>
    [
        new() { CandidateId = "ONB-001", CompanyName = "Northwind Components", LegalEntityType = "Private Company", Country = "Australia", Industry = "Manufacturing", ContactName = "Riley Chen", ContactEmail = "riley.chen@northwind.example", Website = "https://northwind.example" },
        new() { CandidateId = "ONB-002", CompanyName = "Contoso Logistics", LegalEntityType = "Corporation", Country = "United States", Industry = "Transportation", ContactName = "Sam Patel", ContactEmail = "sam.patel@contoso.example", Website = "https://contoso.example" },
        new() { CandidateId = "ONB-003", CompanyName = "Fabrikam Foods", LegalEntityType = "Private Company", Country = "United Kingdom", Industry = "Food & Beverage", ContactName = "Jamie Reed", ContactEmail = "jamie.reed@fabrikam.example", Website = "https://fabrikam.example" },
        new() { CandidateId = "ONB-004", CompanyName = "Tailwind Energy", LegalEntityType = "Corporation", Country = "Canada", Industry = "Utilities", ContactName = "Morgan Diaz", ContactEmail = "morgan.diaz@tailwind.example", Website = "https://tailwind.example" },
        new() { CandidateId = "ONB-005", CompanyName = "Adventure Works Retail", LegalEntityType = "Private Company", Country = "New Zealand", Industry = "Retail", ContactName = "Quinn Foster", ContactEmail = "quinn.foster@adventure-works.example", Website = "https://adventure-works.example" },
        new() { CandidateId = "ONB-006", CompanyName = "Proseware Analytics", LegalEntityType = "Corporation", Country = "Germany", Industry = "Technology", ContactName = "Avery Klein", ContactEmail = "avery.klein@proseware.example", Website = "https://proseware.example" },
        new() { CandidateId = "ONB-007", CompanyName = "Wingtip Textiles", LegalEntityType = "Private Company", Country = "India", Industry = "Textiles", ContactName = "Priya Nair", ContactEmail = "priya.nair@wingtip.example", Website = "https://wingtip.example" },
        new() { CandidateId = "ONB-008", CompanyName = "Coho Financial", LegalEntityType = "Corporation", Country = "Singapore", Industry = "Financial Services", ContactName = "Lee Tan", ContactEmail = "lee.tan@coho.example", Website = "https://coho.example" },
        new() { CandidateId = "ONB-009", CompanyName = "Lucerne Publishing", LegalEntityType = "Private Company", Country = "France", Industry = "Media", ContactName = "Camille Dubois", ContactEmail = "camille.dubois@lucerne.example", Website = "https://lucerne.example" },
        new() { CandidateId = "ONB-010", CompanyName = "Alpine Construction", LegalEntityType = "Corporation", Country = "Switzerland", Industry = "Construction", ContactName = "Noa Meier", ContactEmail = "noa.meier@alpine.example", Website = "https://alpine.example" },
    ];

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
