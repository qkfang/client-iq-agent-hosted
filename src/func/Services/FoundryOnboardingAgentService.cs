using Azure.AI.Agents.Persistent;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Onboarding.FunctionApp.Models;

namespace Onboarding.FunctionApp.Services;

/// <summary>
/// Sends an onboarding form to the Foundry onboarding agent and returns its response.
/// </summary>
public class FoundryOnboardingAgentService
{
    private readonly PersistentAgentsClient _client;
    private readonly FoundryOptions _options;
    private readonly ILogger<FoundryOnboardingAgentService> _logger;

    public FoundryOnboardingAgentService(IOptions<FoundryOptions> options, ILogger<FoundryOnboardingAgentService> logger)
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

        var prompt = $"A new onboarding form named '{fileName}' has arrived. Review the content below and continue the client onboarding workflow.\n\n{formContent}";
        await _client.Messages.CreateMessageAsync(thread.Value.Id, MessageRole.User, prompt, cancellationToken: cancellationToken);

        ThreadRun run = await _client.Runs.CreateRunAsync(thread.Value.Id, _options.OnboardingAgentId, cancellationToken: cancellationToken);
        while (run.Status == RunStatus.Queued || run.Status == RunStatus.InProgress)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            run = await _client.Runs.GetRunAsync(thread.Value.Id, run.Id, cancellationToken);
        }

        if (run.Status != RunStatus.Completed)
        {
            _logger.LogWarning("Onboarding agent run for {FileName} finished with status {Status}", fileName, run.Status);
            return new OnboardingAgentResult(fileName, $"Agent run did not complete successfully (status: {run.Status}).");
        }

        var messages = _client.Messages.GetMessagesAsync(thread.Value.Id, order: ListSortOrder.Descending, cancellationToken: cancellationToken);
        await foreach (var message in messages)
        {
            if (message.Role != MessageRole.Agent)
            {
                continue;
            }

            var textContent = message.ContentItems.OfType<MessageTextContent>().FirstOrDefault();
            if (textContent is not null)
            {
                return new OnboardingAgentResult(fileName, textContent.Text);
            }
        }

        return new OnboardingAgentResult(fileName, "Agent completed but returned no response.");
    }
}
