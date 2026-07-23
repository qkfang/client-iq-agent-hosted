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
    private readonly ProjectResponsesClient _responseClient;
    private readonly ILogger _logger;

    public string AgentId { get; }
    public string Instructions { get; }

    protected BaseAgent(
        AIProjectClient projectClient,
        string agentId,
        string deploymentName,
        string instructions,
        IList<ResponseTool>? tools,
        ILogger logger)
    {
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

        _responseClient = projectClient.ProjectOpenAIClient.GetProjectResponsesClientForAgent(version.Name);
    }

    /// <summary>
    /// Runs the prompt through the agent, auto-approving MCP tool calls, and
    /// returns the final text output.
    /// </summary>
    public async Task<string> RunAsync(string message, CancellationToken cancellationToken = default)
    {
        CreateResponseOptions? nextOptions = new()
        {
            InputItems = { ResponseItem.CreateUserMessageItem(message) },
        };

        ResponseResult? result = null;
        while (nextOptions is not null)
        {
            result = await _responseClient.CreateResponseAsync(nextOptions, cancellationToken);
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
