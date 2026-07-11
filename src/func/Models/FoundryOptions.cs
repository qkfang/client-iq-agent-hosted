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
    /// The id of the onboarding agent to run against each incoming form.
    /// </summary>
    public string OnboardingAgentId { get; set; } = string.Empty;

    /// <summary>
    /// Optional Entra tenant id override for local development.
    /// </summary>
    public string? TenantId { get; set; }
}
