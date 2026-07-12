namespace Onboarding.FunctionApp.Models;

/// <summary>
/// Azure AI Foundry connection settings used to call the onboarding agent.
/// </summary>
public class FoundryOptions
{
    /// <summary>
    /// The Azure AI Foundry project endpoint, e.g. https://{project}.services.ai.azure.com/api/projects/{name}.
    /// </summary>
    public string ProjectEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// The id of the intake agent that extracts a structured JSON summary
    /// from each incoming form.
    /// </summary>
    public string IntakeAgentId { get; set; } = string.Empty;

    /// <summary>
    /// The id of the orchestrator agent that routes the intake summary to a
    /// sub agent.
    /// </summary>
    public string OrchestratorAgentId { get; set; } = string.Empty;

    /// <summary>
    /// The id of the onboarding sub agent (new/existing customer setup).
    /// </summary>
    public string OnboardingAgentId { get; set; } = string.Empty;

    /// <summary>
    /// The id of the opportunity sub agent (sales opportunities, upsells, renewals).
    /// </summary>
    public string OpportunityAgentId { get; set; } = string.Empty;

    /// <summary>
    /// The id of the insight sub agent (reporting and analytics requests).
    /// </summary>
    public string InsightAgentId { get; set; } = string.Empty;

    /// <summary>
    /// The id of the CRM sub agent (customer relationship data updates).
    /// </summary>
    public string CrmAgentId { get; set; } = string.Empty;

    /// <summary>
    /// The id of the Lego sub agent (reusable onboarding workflow building blocks).
    /// </summary>
    public string LegoAgentId { get; set; } = string.Empty;

    /// <summary>
    /// Optional Entra tenant id override for local development.
    /// </summary>
    public string? TenantId { get; set; }
}
