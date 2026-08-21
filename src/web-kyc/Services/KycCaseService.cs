using System.Collections.Concurrent;
using Onboarding.Web.Models;

namespace Onboarding.Web.Services;

/// <summary>
/// In-memory store for the KYC/AML CIP checks. Seeded with a sample customer
/// list at startup; cases are created and progressed by the MCP tools that the
/// Foundry agent calls, keyed on the customer id.
/// </summary>
public class KycCaseService
{
    private readonly ConcurrentDictionary<string, KycCase> _cases = new(StringComparer.OrdinalIgnoreCase);
    private long _version;

    public KycCaseService()
    {
        foreach (var seed in SeedCustomers())
        {
            seed.Version = Interlocked.Increment(ref _version);
            _cases[seed.CustomerId] = seed;
        }
    }

    /// <summary>Global change counter so the UI can poll cheaply for updates.</summary>
    public long CurrentVersion => Interlocked.Read(ref _version);

    public IReadOnlyCollection<KycCase> GetCases() =>
        _cases.Values.OrderBy(c => c.CustomerId, StringComparer.OrdinalIgnoreCase).ToList();

    public KycCase? GetCase(string customerId) => _cases.GetValueOrDefault(customerId);

    /// <summary>
    /// Starts (or restarts) the KYC/AML check for a customer. Creates the customer
    /// entry when the id is not already tracked.
    /// </summary>
    public KycCase StartCase(
        string customerId,
        string? customerName,
        string? jurisdiction,
        string? entityType,
        string? productScope,
        string? businessContact,
        string updatedBy)
    {
        var kycCase = _cases.GetOrAdd(customerId, id => new KycCase { CustomerId = id, CustomerName = customerName ?? id });

        if (!string.IsNullOrWhiteSpace(customerName)) kycCase.CustomerName = customerName;
        if (!string.IsNullOrWhiteSpace(jurisdiction)) kycCase.Jurisdiction = jurisdiction;
        if (!string.IsNullOrWhiteSpace(entityType)) kycCase.EntityType = entityType;
        if (!string.IsNullOrWhiteSpace(productScope)) kycCase.ProductScope = productScope;
        if (!string.IsNullOrWhiteSpace(businessContact)) kycCase.BusinessContact = businessContact;

        kycCase.CaseStatus = "In progress";
        kycCase.FinalStatus = null;
        kycCase.RiskAssessment = null;
        kycCase.CipResult = null;
        kycCase.Requirements = [];
        kycCase.Activity = [];
        kycCase.Stages = BuildStages();
        kycCase.Stages[0].Status = KycStatus.Completed;
        kycCase.Stages[0].Detail = "Onboarding request raised; client, jurisdiction and product scope captured.";
        kycCase.Stages[0].UpdatedUtc = DateTimeOffset.UtcNow;
        kycCase.Stages[1].Status = KycStatus.InProgress;
        kycCase.NextStepsRequired = "Enrich the client record";
        kycCase.ActionableBy = "Agent";
        kycCase.Summary = $"KYC/AML check opened for {kycCase.CustomerName}. CIP evaluation runs against the {kycCase.Jurisdiction} decision tree.";
        kycCase.CreatedUtc = DateTimeOffset.UtcNow;

        AddActivity(kycCase, new KycActivity
        {
            Step = "Case opened",
            Kind = "flow",
            Message = $"KYC/AML case opened for {kycCase.CustomerName} ({kycCase.CustomerId}).",
            Status = KycStatus.Completed,
            Actor = updatedBy
        });

        Touch(kycCase, updatedBy);
        return kycCase;
    }

    public KycCase? UpdateStage(string customerId, string stageKey, string status, string? detail, string updatedBy)
    {
        var kycCase = GetCase(customerId);
        var stage = kycCase?.Stages.FirstOrDefault(s => string.Equals(s.Key, stageKey, StringComparison.OrdinalIgnoreCase));
        if (kycCase is null || stage is null)
        {
            return null;
        }

        stage.Status = status;
        stage.UpdatedUtc = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(detail))
        {
            stage.Detail = detail;
        }

        // Keep the pipeline moving: completing a stage starts the next pending
        // one, unless another stage is already running.
        if (status == KycStatus.Completed)
        {
            var active = kycCase.Stages.FirstOrDefault(s => s != stage && s.Status == KycStatus.InProgress);
            if (active is null)
            {
                active = kycCase.Stages.SkipWhile(s => s != stage).Skip(1).FirstOrDefault(s => s.Status == KycStatus.Pending);
                if (active is not null)
                {
                    active.Status = KycStatus.InProgress;
                }
            }

            if (active is not null)
            {
                kycCase.NextStepsRequired = active.Label;
                kycCase.ActionableBy = active.Owner.Contains("Agent", StringComparison.OrdinalIgnoreCase) ? "Agent" : "Human";
            }
        }
        else if (status == KycStatus.Blocked)
        {
            kycCase.CaseStatus = "Blocked";
            kycCase.NextStepsRequired = $"Resolve blocker on {stage.Label}";
            kycCase.ActionableBy = "Human";
        }

        if (status != KycStatus.Blocked && kycCase.FinalStatus is null && kycCase.Stages.All(s => s.Status != KycStatus.Blocked))
        {
            kycCase.CaseStatus = "In progress";
        }

        AddActivity(kycCase, new KycActivity
        {
            Step = stage.Label,
            Kind = "flow",
            Message = detail ?? $"{stage.Label} marked {status}.",
            Status = status,
            Actor = updatedBy
        });

        Touch(kycCase, updatedBy);
        return kycCase;
    }

    public KycCase? SetRiskAssessment(string customerId, KycRiskAssessment assessment, string updatedBy)
    {
        var kycCase = GetCase(customerId);
        if (kycCase is null)
        {
            return null;
        }

        assessment.UpdatedUtc = DateTimeOffset.UtcNow;
        kycCase.RiskAssessment = assessment;

        AddActivity(kycCase, new KycActivity
        {
            Step = "Risk assessment",
            Kind = "deep-reasoning",
            Message = $"Risk rating {assessment.RiskRating} with a total score of {assessment.TotalScore} of {assessment.MaxPossibleScore}.",
            Status = KycStatus.Completed,
            Actor = updatedBy
        });

        UpdateStage(customerId, "risk", assessment.ApprovalState == "Approved" ? KycStatus.Completed : KycStatus.InProgress,
            $"Risk rating {assessment.RiskRating}; approval {assessment.ApprovalState}.", updatedBy);

        if (assessment.ApprovalState == "Pending")
        {
            kycCase.NextStepsRequired = "Approve risk assessment";
            kycCase.ActionableBy = "Human";
        }

        Touch(kycCase, updatedBy);
        return kycCase;
    }

    public KycCase? SetCipResult(string customerId, KycCipResult result, string updatedBy)
    {
        var kycCase = GetCase(customerId);
        if (kycCase is null)
        {
            return null;
        }

        result.UpdatedUtc = DateTimeOffset.UtcNow;
        kycCase.CipResult = result;

        if (kycCase.Requirements.Count == 0)
        {
            var source = $"CIP Schedule {result.ClauseNumber}";
            kycCase.Requirements = RequirementCatalog
                .Select(r => new KycRequirement { Id = r.Id, Requirement = r.Requirement, Group = r.Group, Source = source })
                .ToList();
        }

        AddActivity(kycCase, new KycActivity
        {
            Step = "CIP schedule decision tree",
            Kind = "knowledge",
            Message = result.Conclusion,
            Status = KycStatus.Completed,
            Actor = updatedBy
        });

        kycCase.NextStepsRequired = "Approve AML requirements";
        kycCase.ActionableBy = "Human";

        Touch(kycCase, updatedBy);
        return kycCase;
    }

    public KycCase? SetApproval(string customerId, string target, string state, string reviewer)
    {
        var kycCase = GetCase(customerId);
        if (kycCase is null)
        {
            return null;
        }

        var isRisk = string.Equals(target, "risk", StringComparison.OrdinalIgnoreCase);
        if (isRisk && kycCase.RiskAssessment is not null)
        {
            kycCase.RiskAssessment.ApprovalState = state;
            kycCase.RiskAssessment.Reviewer = reviewer;
        }
        else if (!isRisk && kycCase.CipResult is not null)
        {
            kycCase.CipResult.ApprovalState = state;
            kycCase.CipResult.Reviewer = reviewer;
        }

        var stageKey = isRisk ? "risk" : "amlRequirements";
        var label = isRisk ? "risk assessment" : "AML requirements";
        UpdateStage(customerId, stageKey, state == "Approved" ? KycStatus.Completed : KycStatus.Blocked,
            $"{reviewer} {state.ToLowerInvariant()} the {label}.", reviewer);

        Touch(kycCase, reviewer);
        return kycCase;
    }

    public KycCase? UpdateRequirement(string customerId, int requirementId, string status, string? evidence, string? owner, string updatedBy)
    {
        var kycCase = GetCase(customerId);
        var requirement = kycCase?.Requirements.FirstOrDefault(r => r.Id == requirementId);
        if (kycCase is null || requirement is null)
        {
            return null;
        }

        requirement.Status = status;
        if (!string.IsNullOrWhiteSpace(evidence)) requirement.Evidence = evidence;
        if (!string.IsNullOrWhiteSpace(owner)) requirement.Owner = owner;

        AddActivity(kycCase, new KycActivity
        {
            Step = $"Requirement {requirementId}",
            Kind = "flow",
            Message = $"{requirement.Requirement} - {status}.",
            Status = status,
            Actor = updatedBy
        });

        Touch(kycCase, updatedBy);
        return kycCase;
    }

    public KycCase? CompleteCase(string customerId, string finalStatus, string? notes, string updatedBy)
    {
        var kycCase = GetCase(customerId);
        if (kycCase is null)
        {
            return null;
        }

        kycCase.FinalStatus = finalStatus;
        kycCase.CaseStatus = finalStatus;
        kycCase.NextStepsRequired = "None";
        kycCase.ActionableBy = "None";
        foreach (var stage in kycCase.Stages.Where(s => s.Status != KycStatus.Blocked))
        {
            stage.Status = KycStatus.Completed;
        }

        AddActivity(kycCase, new KycActivity
        {
            Step = "Case closed",
            Kind = "flow",
            Message = notes ?? $"KYC/AML check closed as {finalStatus}.",
            Status = KycStatus.Completed,
            Actor = updatedBy
        });

        Touch(kycCase, updatedBy);
        return kycCase;
    }

    public KycCase? LogActivity(string customerId, KycActivity activity)
    {
        var kycCase = GetCase(customerId);
        if (kycCase is null)
        {
            return null;
        }

        AddActivity(kycCase, activity);
        Touch(kycCase, activity.Actor);
        return kycCase;
    }

    public bool RemoveCase(string customerId) => _cases.TryRemove(customerId, out _);

    private static void AddActivity(KycCase kycCase, KycActivity activity)
    {
        activity.TimestampUtc = DateTimeOffset.UtcNow;
        kycCase.Activity.Insert(0, activity);
        if (kycCase.Activity.Count > 60)
        {
            kycCase.Activity.RemoveRange(60, kycCase.Activity.Count - 60);
        }
    }

    private void Touch(KycCase kycCase, string updatedBy)
    {
        kycCase.LastUpdatedBy = updatedBy;
        kycCase.LastUpdatedUtc = DateTimeOffset.UtcNow;
        kycCase.Version = Interlocked.Increment(ref _version);
    }

    private static List<KycStage> BuildStages() =>
    [
        new() { Key = "initiate", Label = "Initiate", Owner = "Business contact" },
        new() { Key = "enrichment", Label = "Data Enrichment", Owner = "Data Enrichment Agent" },
        new() { Key = "risk", Label = "Risk Calculation", Owner = "Risk Assessment Agent" },
        new() { Key = "amlRequirements", Label = "AML Requirements", Owner = "CIP Evaluation Agent" },
        new() { Key = "sourcing", Label = "Sourcing", Owner = "Analyst + Agent" },
        new() { Key = "gapAnalysis", Label = "Gap Analysis", Owner = "Agent" },
        new() { Key = "outreach", Label = "Client Outreach", Owner = "Analyst" },
    ];

    private static IEnumerable<KycCase> SeedCustomers() =>
    [
        new() { CustomerId = "CUST-1001", CustomerName = "Contoso Capital Markets", Jurisdiction = "HK", EntityType = "Regulated Financial Institution", ProductScope = "Derivatives - OTC", BusinessContact = "Avery Klein", Regulator = "Prudential Regulator A", ListingExchange = "Exchange A" },
        new() { CustomerId = "CUST-1002", CustomerName = "Fabrikam Global Fund", Jurisdiction = "HK", EntityType = "Fund / Collective Investment Scheme", ProductScope = "Securities - Cash", BusinessContact = "Riley Chen" },
        new() { CustomerId = "CUST-1003", CustomerName = "Northwind Energy Holdings", Jurisdiction = "HK", EntityType = "Public Listed Company", ProductScope = "FX - Spot & Forward", BusinessContact = "Morgan Diaz", ListingExchange = "Exchange B" },
        new() { CustomerId = "CUST-1004", CustomerName = "Adventure Works Pension Scheme", Jurisdiction = "HK", EntityType = "Pension Scheme", ProductScope = "Securities - Cash", BusinessContact = "Quinn Foster" },
        new() { CustomerId = "CUST-1005", CustomerName = "Proseware Partners LLP", Jurisdiction = "HK", EntityType = "Partnership", ProductScope = "Lending - Facility", BusinessContact = "Jamie Reed" },
        new() { CustomerId = "CUST-1006", CustomerName = "Coho Sovereign Authority", Jurisdiction = "HK", EntityType = "Government / Wholly State-Owned Entity", ProductScope = "Money Markets", BusinessContact = "Lee Tan" },
    ];

    /// <summary>CIP schedule requirement catalog (sections 4.4 of the process spec).</summary>
    private static readonly KycRequirement[] RequirementCatalog =
    [
        new() { Id = 1, Group = "Entity identification", Requirement = "Full legal name" },
        new() { Id = 2, Group = "Entity identification", Requirement = "Registered office address in the place of incorporation" },
        new() { Id = 3, Group = "Entity identification", Requirement = "Principal place of business" },
        new() { Id = 4, Group = "Entity identification", Requirement = "Confirmation of regulation (including parent / head office)" },
        new() { Id = 5, Group = "Entity identification", Requirement = "Registration number" },
        new() { Id = 6, Group = "Entity identification", Requirement = "Country of incorporation" },
        new() { Id = 7, Group = "Entity identification", Requirement = "Date of incorporation" },
        new() { Id = 8, Group = "Entity identification", Requirement = "Nature of business" },
        new() { Id = 9, Group = "Entity identification", Requirement = "Purpose and nature of the relationship / investment" },
        new() { Id = 10, Group = "Entity identification", Requirement = "Source of funds (as applicable)" },
        new() { Id = 11, Group = "Entity identification", Requirement = "Customer acting capacity (principal / agent) & AML reliance letter" },
        new() { Id = 12, Group = "Entity identification", Requirement = "Cross-border relationship due-diligence questionnaire & regional financial-crime approval" },
        new() { Id = 13, Group = "Entity identification", Requirement = "Business management approval" },
        new() { Id = 14, Group = "People, ownership & screening", Requirement = "Names of persons authorised to act on behalf of the customer" },
        new() { Id = 15, Group = "People, ownership & screening", Requirement = "KYC & verification of persons authorised to instruct" },
        new() { Id = 16, Group = "People, ownership & screening", Requirement = "Names of all directors" },
        new() { Id = 17, Group = "People, ownership & screening", Requirement = "Name screening (customer, directors, authorised persons)" },
        new() { Id = 18, Group = "People, ownership & screening", Requirement = "KYC of beneficial owners holding > 10%" },
        new() { Id = 19, Group = "People, ownership & screening", Requirement = "Alternate requirement if no beneficial owner holds > 10%" },
        new() { Id = 20, Group = "People, ownership & screening", Requirement = "Beneficial owners: occupation & source of wealth" },
        new() { Id = 21, Group = "People, ownership & screening", Requirement = "Nominee shareholders: confirm ultimate ownership" },
        new() { Id = 22, Group = "People, ownership & screening", Requirement = "KYC & verification of at least one director" },
        new() { Id = 23, Group = "People, ownership & screening", Requirement = "Additional name screening (beneficial owners and connected parties)" },
        new() { Id = 24, Group = "People, ownership & screening", Requirement = "Media searches on the customer and connected parties" },
        new() { Id = 25, Group = "People, ownership & screening", Requirement = "Regional financial-crime review after business approval" },
    ];
}
