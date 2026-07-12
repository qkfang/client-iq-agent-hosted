using System.Text.Json;
using Azure.AI.Agents.Persistent;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Onboarding.FunctionApp.Models;

namespace Onboarding.FunctionApp.Services;

/// <summary>
/// Runs an incoming onboarding form through the Foundry agent pipeline:
/// IntakeAgent extracts a structured JSON summary, OrchestratorAgent decides
/// which sub agent should handle it, and that sub agent produces the final
/// response.
/// </summary>
public class FoundryOnboardingPipelineService
{
    private readonly PersistentAgentsClient _client;
    private readonly FoundryOptions _options;
    private readonly ILogger<FoundryOnboardingPipelineService> _logger;

    public FoundryOnboardingPipelineService(IOptions<FoundryOptions> options, ILogger<FoundryOnboardingPipelineService> logger)
    {
        _options = options.Value;
        _logger = logger;

        var credentialOptions = new DefaultAzureCredentialOptions();
        if (!string.IsNullOrWhiteSpace(_options.TenantId))
        {
            credentialOptions.TenantId = _options.TenantId;
        }

        _client = new PersistentAgentsClient(_options.ProjectEndpoint, new DefaultAzureCredential(credentialOptions));
    }

    public async Task<OnboardingAgentResult> ProcessOnboardingFormAsync(string fileName, string formContent, CancellationToken cancellationToken = default)
    {
        var thread = await _client.Threads.CreateThreadAsync(cancellationToken: cancellationToken);
        var threadId = thread.Value.Id;

        var intakePrompt = $"A new onboarding form named '{fileName}' has arrived. Extract its intake summary.\n\n{formContent}";
        var intakeJson = await RunAgentAsync(threadId, _options.IntakeAgentId, intakePrompt, cancellationToken);

        var orchestratorPrompt = $"Route the following intake summary.\n\n{intakeJson}";
        var orchestratorJson = await RunAgentAsync(threadId, _options.OrchestratorAgentId, orchestratorPrompt, cancellationToken);

        var route = ExtractRoute(orchestratorJson);
        var subAgentId = ResolveSubAgentId(route);
        if (string.IsNullOrWhiteSpace(subAgentId))
        {
            _logger.LogWarning("No sub agent configured for route '{Route}' (file {FileName})", route, fileName);
            return new OnboardingAgentResult(fileName, route, "No sub agent configured for this route.");
        }

        var subAgentPrompt = $"Handle the following onboarding intake summary.\n\n{intakeJson}";
        var subAgentResponse = await RunAgentAsync(threadId, subAgentId, subAgentPrompt, cancellationToken);

        return new OnboardingAgentResult(fileName, route, subAgentResponse);
    }

    /// <summary>
    /// Posts a prompt to the given agent on the shared thread and returns its
    /// text response once the run completes.
    /// </summary>
    private async Task<string> RunAgentAsync(string threadId, string agentId, string prompt, CancellationToken cancellationToken)
    {
        await _client.Messages.CreateMessageAsync(threadId, MessageRole.User, prompt, cancellationToken: cancellationToken);

        ThreadRun run = await _client.Runs.CreateRunAsync(threadId, agentId, cancellationToken: cancellationToken);
        while (run.Status == RunStatus.Queued || run.Status == RunStatus.InProgress)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            run = await _client.Runs.GetRunAsync(threadId, run.Id, cancellationToken);
        }

        if (run.Status != RunStatus.Completed)
        {
            _logger.LogWarning("Agent run for {AgentId} finished with status {Status}", agentId, run.Status);
            return $"Agent run did not complete successfully (status: {run.Status}).";
        }

        var messages = _client.Messages.GetMessagesAsync(threadId, order: ListSortOrder.Descending, cancellationToken: cancellationToken);
        await foreach (var message in messages)
        {
            if (message.Role != MessageRole.Agent)
            {
                continue;
            }

            var textContent = message.ContentItems.OfType<MessageTextContent>().FirstOrDefault();
            if (textContent is not null)
            {
                return textContent.Text;
            }
        }

        return "Agent completed but returned no response.";
    }

    /// <summary>
    /// Reads the "route" field from OrchestratorAgent's JSON response.
    /// Falls back to "onboarding" if the response cannot be parsed.
    /// </summary>
    private string ExtractRoute(string orchestratorJson)
    {
        try
        {
            using var document = JsonDocument.Parse(orchestratorJson);
            if (document.RootElement.TryGetProperty("route", out var routeElement))
            {
                return routeElement.GetString() ?? "onboarding";
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Could not parse orchestrator response as JSON: {Response}", orchestratorJson);
        }

        return "onboarding";
    }

    private string ResolveSubAgentId(string route) => route switch
    {
        "onboarding" => _options.OnboardingAgentId,
        "opportunity" => _options.OpportunityAgentId,
        "insight" => _options.InsightAgentId,
        "crm" => _options.CrmAgentId,
        "lego" => _options.LegoAgentId,
        _ => _options.OnboardingAgentId,
    };
}
