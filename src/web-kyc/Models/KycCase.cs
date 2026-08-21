namespace Onboarding.Web.Models;

/// <summary>
/// A customer tracked through the KYC/AML CIP check. The customer id is the
/// tracking key shared between the UI and the Foundry agent MCP calls.
/// </summary>
public class KycCase
{
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Jurisdiction { get; set; } = "HK";
    public string EntityType { get; set; } = string.Empty;
    public string ProductScope { get; set; } = string.Empty;
    public string BusinessContact { get; set; } = string.Empty;
    public string Regulator { get; set; } = string.Empty;
    public string ListingExchange { get; set; } = string.Empty;

    public string CaseStatus { get; set; } = "Not started";
    public string Summary { get; set; } = string.Empty;
    public string? FinalStatus { get; set; }

    public List<KycStage> Stages { get; set; } = [];
    public KycRiskAssessment? RiskAssessment { get; set; }
    public KycCipResult? CipResult { get; set; }
    public List<KycRequirement> Requirements { get; set; } = [];
    public List<KycActivity> Activity { get; set; } = [];

    public string LastUpdatedBy { get; set; } = "System";
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastUpdatedUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Incremented on every change so the UI can poll for deltas.</summary>
    public long Version { get; set; }

    public string CurrentStage =>
        Stages.FirstOrDefault(s => s.Status == KycStatus.InProgress)?.Label
        ?? Stages.FirstOrDefault(s => s.Status == KycStatus.Blocked)?.Label
        ?? Stages.FirstOrDefault(s => s.Status == KycStatus.Pending)?.Label
        ?? "Complete";

    public string NextStepsRequired { get; set; } = string.Empty;
    public string ActionableBy { get; set; } = "Agent";

    /// <summary>Ready-to-trade percentage: stage progress plus requirement coverage.</summary>
    public int ReadinessPercent
    {
        get
        {
            if (Stages.Count == 0)
            {
                return 0;
            }

            var stageScore = Stages.Sum(s => s.Status switch
            {
                KycStatus.Completed => 1.0,
                KycStatus.InProgress => 0.5,
                _ => 0.0
            }) / Stages.Count;

            if (Requirements.Count == 0)
            {
                return (int)Math.Round(stageScore * 100);
            }

            var satisfied = Requirements.Count(r =>
                r.Status is KycStatus.Satisfied or KycStatus.Waived or KycStatus.NotApplicable);
            var requirementScore = (double)satisfied / Requirements.Count;

            return (int)Math.Round((stageScore * 0.6 + requirementScore * 0.4) * 100);
        }
    }
}

public class KycStage
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Status { get; set; } = KycStatus.Pending;
    public string Detail { get; set; } = string.Empty;
    public DateTimeOffset? UpdatedUtc { get; set; }
}

public class KycRiskAssessment
{
    public string RiskRating { get; set; } = "Not assessed";
    public int TotalScore { get; set; }
    public int MaxPossibleScore { get; set; } = 112;
    public List<KycRiskCategory> Categories { get; set; } = [];
    public string ApprovalState { get; set; } = "Pending";
    public string? Reviewer { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
}

public class KycRiskCategory
{
    public string Name { get; set; } = string.Empty;
    public int Score { get; set; }
    public int Max { get; set; }
    public string Indicator { get; set; } = "green";
    public string Reason { get; set; } = string.Empty;
}

public class KycCipResult
{
    public string ClauseNumber { get; set; } = string.Empty;
    public string ClauseName { get; set; } = string.Empty;
    public List<KycReasoningStep> StepByStepReasoning { get; set; } = [];
    public string Conclusion { get; set; } = string.Empty;
    public List<string> Sources { get; set; } = [];
    public string ApprovalState { get; set; } = "Pending";
    public string? Reviewer { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
}

public class KycReasoningStep
{
    public int Step { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public string Reasoning { get; set; } = string.Empty;
}

public class KycRequirement
{
    public int Id { get; set; }
    public string Requirement { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Status { get; set; } = KycStatus.Outstanding;
    public string Owner { get; set; } = "Analyst";
    public string? Evidence { get; set; }
    public string? DueDate { get; set; }
}

public class KycActivity
{
    public string Step { get; set; } = string.Empty;
    public string Kind { get; set; } = "flow";
    public string Message { get; set; } = string.Empty;
    public string Status { get; set; } = KycStatus.Completed;
    public string Actor { get; set; } = "Agent";
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Status vocabulary shared by stages, requirements and activity steps.</summary>
public static class KycStatus
{
    public const string Completed = "Completed";
    public const string InProgress = "In Progress";
    public const string Pending = "Pending";
    public const string Blocked = "Blocked";

    public const string Satisfied = "Satisfied";
    public const string InReview = "In Review";
    public const string Outstanding = "Outstanding";
    public const string Waived = "Waived";
    public const string NotApplicable = "Not Applicable";
}
