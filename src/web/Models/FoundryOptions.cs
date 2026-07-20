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
    /// Knowledge Base and Work IQ MCP tools reached through a Foundry project
    /// connection (Azure AI Search knowledge bases, hosted Work IQ, etc.).
    /// </summary>
    public List<McpConnectedTool> ConnectedTools { get; set; } = new();

    /// <summary>
    /// When true, the built-in web search tool is attached to the agent.
    /// </summary>
    public bool EnableWebSearch { get; set; }

    /// <summary>
    /// Optional Entra tenant id override for local development.
    /// </summary>
    public string? TenantId { get; set; }
}

/// <summary>
/// A remote MCP tool that authenticates through a Foundry project connection,
/// such as an Azure AI Search Knowledge Base or the hosted Work IQ service.
/// </summary>
public class McpConnectedTool
{
    /// <summary>Label the agent uses to identify the MCP server.</summary>
    public string ServerLabel { get; set; } = string.Empty;

    /// <summary>Full MCP endpoint URL for the tool.</summary>
    public string ServerUrl { get; set; } = string.Empty;

    /// <summary>Foundry project connection that holds the tool's credentials.</summary>
    public string ProjectConnectionId { get; set; } = string.Empty;
}
