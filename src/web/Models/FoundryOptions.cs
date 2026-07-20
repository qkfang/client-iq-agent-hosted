namespace Onboarding.Web.Models;

/// <summary>
/// Azure AI Foundry connection settings used by the web app to invoke the
/// SalesCRMOnboarding agent.
/// </summary>
public class FoundryOptions
{
    /// <summary>
    /// The Azure AI Foundry project endpoint, e.g.
    /// https://{project}.services.ai.azure.com/api/projects/{name}.
    /// </summary>
    public string ProjectEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// The id of the SalesCRMOnboarding agent that researches candidates and
    /// finalises CRM records via the /mcp endpoint.
    /// </summary>
    public string SalesCrmOnboardingAgentId { get; set; } = string.Empty;

    /// <summary>
    /// Optional Entra tenant id override for local development.
    /// </summary>
    public string? TenantId { get; set; }
}
