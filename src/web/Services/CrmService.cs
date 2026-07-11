using System.Collections.Concurrent;
using Onboarding.Web.Models;

namespace Onboarding.Web.Services;

/// <summary>
/// In-memory stand-in for the Dynamics 365 CRM. Seeded with sample customer
/// records so the mock UI has something to show, and updated by the MCP
/// tools that the Foundry onboarding agent calls.
/// </summary>
public class CrmService
{
    private readonly ConcurrentDictionary<string, CrmRecord> _records = new();

    public CrmService()
    {
        Seed(new CrmRecord
        {
            CustomerId = "CUST-1001",
            CustomerName = "Sample Trading Pty Ltd",
            LegalEntityType = "Private Company",
            Country = "Australia",
            OnboardingStatus = "In progress",
            KycRiskRating = "Not assessed",
            Notes = "Awaiting onboarding form review."
        });

        Seed(new CrmRecord
        {
            CustomerId = "CUST-1002",
            CustomerName = "Example Holdings Inc",
            LegalEntityType = "Corporation",
            Country = "United States",
            OnboardingStatus = "Pending",
            KycRiskRating = "Not assessed",
            Notes = "New client, no documents received yet."
        });
    }

    public IReadOnlyCollection<CrmRecord> GetAll() => _records.Values.OrderBy(r => r.CustomerId).ToList();

    public CrmRecord? Get(string customerId) => _records.GetValueOrDefault(customerId);

    public CrmRecord Upsert(CrmRecord record)
    {
        record.LastUpdatedUtc = DateTimeOffset.UtcNow;
        _records[record.CustomerId] = record;
        return record;
    }

    private void Seed(CrmRecord record) => _records[record.CustomerId] = record;
}
