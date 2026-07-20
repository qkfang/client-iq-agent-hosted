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
    /// The id/name of the SalesCRMOnboarding agent that researches candidates
    /// and finalises CRM records via the /mcp endpoint.
    /// </summary>
    public string SalesCrmOnboardingAgentId { get; set; } = string.Empty;

    /// <summary>
    /// The chat model deployment name used when creating the agent version.
    /// </summary>
    public string ModelDeploymentName { get; set; } = "gpt-5-mini";

    /// <summary>
    /// The publicly reachable URL of this web app's MCP endpoint that the
    /// agent calls back to finalise onboarding, e.g. https://{app}/mcp.
    /// </summary>
    public string WebAppMcpUrl { get; set; } = string.Empty;

    /// <summary>
    /// Optional Entra tenant id override for local development.
    /// </summary>
    public string? TenantId { get; set; }
}
