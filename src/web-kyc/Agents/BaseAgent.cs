using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using OpenAI.Responses;

namespace Onboarding.Web.Agents;

/// <summary>
/// Base class for Foundry agents. Creates an agent version wired to the given
/// tools, runs a prompt through the Responses API, and auto-approves any MCP
/// tool calls the agent makes.
/// </summary>
public abstract class BaseAgent
{
    private readonly AIProjectClient _projectClient;
    private readonly ILogger _logger;

    public string AgentId { get; }
    public string AgentVersionName { get; }
    public string Instructions { get; }

    protected BaseAgent(
        AIProjectClient projectClient,
        string agentId,
        string deploymentName,
        string instructions,
        IList<ResponseTool>? tools,
        ILogger logger)
    {
        _projectClient = projectClient;
        AgentId = agentId;
        Instructions = instructions;
        _logger = logger;

        var definition = new DeclarativeAgentDefinition(model: deploymentName)
        {
            Instructions = instructions,
        };

        if (tools is not null)
        {
            foreach (var tool in tools)
            {
                definition.Tools.Add(tool);
            }
        }

        var version = projectClient.AgentAdministrationClient.CreateAgentVersion(
            agentId,
            new ProjectsAgentVersionCreationOptions(definition)).Value;

        AgentVersionName = version.Name;
    }

    /// <summary>
    /// Runs the prompt using the app identity. Work IQ tool calls run in the
    /// context of that service identity, not an end user.
    /// </summary>
    public Task<string> RunAsync(string message, CancellationToken cancellationToken = default)
        => RunAsync(_projectClient, message, cancellationToken);

    /// <summary>
    /// Runs the prompt using the supplied project client. Pass a client built
    /// from the signed-in user's token so tools such as Work IQ resolve the
    /// current user's Microsoft 365 context via On-Behalf-Of.
    /// </summary>
    public async Task<string> RunAsync(AIProjectClient projectClient, string message, CancellationToken cancellationToken = default)
    {
        var responseClient = projectClient.ProjectOpenAIClient.GetProjectResponsesClientForAgent(AgentVersionName);

        CreateResponseOptions? nextOptions = new()
        {
            InputItems = { ResponseItem.CreateUserMessageItem(message) },
        };

        ResponseResult? result = null;
        while (nextOptions is not null)
        {
            result = await responseClient.CreateResponseAsync(nextOptions, cancellationToken);
            nextOptions = null;

            foreach (var item in result.OutputItems)
            {
                if (item is McpToolCallApprovalRequestItem mcpCall)
                {
                    _logger.LogInformation("Auto-approving MCP tool call on {ServerLabel}", mcpCall.ServerLabel);
                    nextOptions ??= new CreateResponseOptions { PreviousResponseId = result.Id };
                    nextOptions.InputItems.Add(ResponseItem.CreateMcpApprovalResponseItem(mcpCall.Id, approved: true));
                }
            }
        }

        return result?.GetOutputText() ?? string.Empty;
    }
}
