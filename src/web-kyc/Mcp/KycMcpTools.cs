using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Onboarding.Web.Models;
using Onboarding.Web.Services;

namespace Onboarding.Web.Mcp;

/// <summary>
/// MCP tools the Foundry agent calls to open and progress a customer's KYC/AML
/// CIP check. Every call is keyed on the customer id and pushes straight into
/// the tracking UI.
/// </summary>
[McpServerToolType]
public class KycMcpTools
{
    private const string AgentActor = "Foundry KYC agent";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly KycCaseService _cases;

    public KycMcpTools(KycCaseService cases)
    {
        _cases = cases;
    }

    [McpServerTool(Name = "list_kyc_customers"), Description("List the customers tracked for KYC/AML with their case status and readiness percentage.")]
    public string ListCustomers() => Json(_cases.GetCases().Select(c => new
    {
        c.CustomerId,
        c.CustomerName,
        c.Jurisdiction,
        c.EntityType,
        c.CaseStatus,
        c.CurrentStage,
        c.ReadinessPercent,
        c.LastUpdatedUtc
    }));

    [McpServerTool(Name = "start_kyc_case"), Description("Open the KYC/AML CIP check for a customer. Creates the customer entry when the id is not yet tracked.")]
    public string StartCase(
        [Description("Tracking key for the customer, e.g. CUST-1001.")] string customerId,
        [Description("Client legal name.")] string? customerName = null,
        [Description("CIP jurisdiction code, e.g. HK.")] string? jurisdiction = null,
        [Description("Entity type, e.g. Regulated Financial Institution, Public Listed Company, Fund, Pension Scheme, Partnership.")] string? entityType = null,
        [Description("Products in scope, e.g. Derivatives - OTC.")] string? productScope = null,
        [Description("Business contact requesting the onboarding.")] string? businessContact = null)
        => Json(_cases.StartCase(customerId, customerName, jurisdiction, entityType, productScope, businessContact, AgentActor));

    [McpServerTool(Name = "get_kyc_case"), Description("Get the full KYC/AML case for a customer, including stages, risk assessment, CIP result and requirements.")]
    public string GetCase([Description("Tracking key for the customer, e.g. CUST-1001.")] string customerId)
        => Result(_cases.GetCase(customerId), customerId);

    [McpServerTool(Name = "update_kyc_stage"), Description("Set the status of one KYC/AML stage.")]
    public string UpdateStage(
        [Description("Tracking key for the customer.")] string customerId,
        [Description("Stage key: initiate, enrichment, risk, amlRequirements, sourcing, gapAnalysis or outreach.")] string stageKey,
        [Description("Status: Completed, In Progress, Pending or Blocked.")] string status,
        [Description("Short description of what happened in this stage.")] string? detail = null)
        => Result(_cases.UpdateStage(customerId, stageKey, status, detail, AgentActor), customerId);

    [McpServerTool(Name = "log_kyc_activity"), Description("Append a step to the agent activity map so the UI shows the work in progress.")]
    public string LogActivity(
        [Description("Tracking key for the customer.")] string customerId,
        [Description("Step name, e.g. Search sources.")] string step,
        [Description("What the step did or found.")] string message,
        [Description("Step kind: flow, knowledge, deep-reasoning or review.")] string kind = "flow",
        [Description("Status: In Progress or Completed.")] string status = "Completed")
        => Result(_cases.LogActivity(customerId, new KycActivity
        {
            Step = step,
            Message = message,
            Kind = kind,
            Status = status,
            Actor = AgentActor
        }), customerId);

    [McpServerTool(Name = "submit_risk_assessment"), Description("Publish the client risk assessment: total score, rating and per-category scores with reasons.")]
    public string SubmitRiskAssessment(
        [Description("Tracking key for the customer.")] string customerId,
        [Description("Risk rating: Low, Medium or High.")] string riskRating,
        [Description("Total score across all categories; may be negative when mitigating factors apply.")] int totalScore,
        [Description("JSON array of categories: [{\"name\":\"Industry Risk\",\"score\":1,\"max\":65,\"indicator\":\"green\",\"reason\":\"...\"}].")] string categoriesJson,
        [Description("Maximum possible score across all categories.")] int maxPossibleScore = 112)
    {
        var assessment = new KycRiskAssessment
        {
            RiskRating = riskRating,
            TotalScore = totalScore,
            MaxPossibleScore = maxPossibleScore,
            Categories = Parse<List<KycRiskCategory>>(categoriesJson) ?? []
        };

        return Result(_cases.SetRiskAssessment(customerId, assessment, AgentActor), customerId);
    }

    [McpServerTool(Name = "submit_cip_result"), Description("Publish the CIP schedule selected by the decision tree, its step-by-step reasoning, and expand it into the requirement list.")]
    public string SubmitCipResult(
        [Description("Tracking key for the customer.")] string customerId,
        [Description("CIP clause number, e.g. 4.9.")] string clauseNumber,
        [Description("CIP clause name, e.g. Regulated Entity & Branches (Cross-border Correspondent Relationship).")] string clauseName,
        [Description("Conclusion sentence stating which CIP schedule to apply.")] string conclusion,
        [Description("JSON array of reasoning steps: [{\"step\":1,\"question\":\"...\",\"answer\":\"Yes\",\"reasoning\":\"...\"}].")] string reasoningJson,
        [Description("JSON array of source names, e.g. [\"HK CIP Schedule decision tree\"].")] string? sourcesJson = null)
    {
        var result = new KycCipResult
        {
            ClauseNumber = clauseNumber,
            ClauseName = clauseName,
            Conclusion = conclusion,
            StepByStepReasoning = Parse<List<KycReasoningStep>>(reasoningJson) ?? [],
            Sources = Parse<List<string>>(sourcesJson) ?? []
        };

        return Result(_cases.SetCipResult(customerId, result, AgentActor), customerId);
    }

    [McpServerTool(Name = "update_kyc_requirement"), Description("Set the status and evidence of one CIP requirement.")]
    public string UpdateRequirement(
        [Description("Tracking key for the customer.")] string customerId,
        [Description("Requirement id, 1 to 25.")] int requirementId,
        [Description("Status: Satisfied, In Review, Outstanding, Waived or Not Applicable.")] string status,
        [Description("Evidence held for the requirement.")] string? evidence = null,
        [Description("Owner responsible for closing the requirement.")] string? owner = null)
        => Result(_cases.UpdateRequirement(customerId, requirementId, status, evidence, owner, AgentActor), customerId);

    [McpServerTool(Name = "set_kyc_approval"), Description("Record the human review decision on the risk assessment or the AML requirements.")]
    public string SetApproval(
        [Description("Tracking key for the customer.")] string customerId,
        [Description("Approval target: risk or requirements.")] string target,
        [Description("Decision: Approved or Rejected.")] string state,
        [Description("Name of the reviewer.")] string reviewer)
        => Result(_cases.SetApproval(customerId, target, state, reviewer), customerId);

    [McpServerTool(Name = "complete_kyc_case"), Description("Close the KYC/AML check, e.g. Ready to trade.")]
    public string CompleteCase(
        [Description("Tracking key for the customer.")] string customerId,
        [Description("Final status, e.g. Ready to trade or Escalated.")] string finalStatus,
        [Description("Closing notes.")] string? notes = null)
        => Result(_cases.CompleteCase(customerId, finalStatus, notes, AgentActor), customerId);

    private static string Result(KycCase? kycCase, string customerId) =>
        kycCase is null
            ? Json(new { error = $"No KYC case found for {customerId}. Call start_kyc_case first." })
            : Json(kycCase);

    private static string Json(object value) => JsonSerializer.Serialize(value, JsonOptions);

    private static T? Parse<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }
}
