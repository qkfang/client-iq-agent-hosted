using System.ClientModel.Primitives;
using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Extensions.Options;
using Onboarding.Web.Models;
using OpenAI.Responses;

namespace Onboarding.Web.Services;

/// <summary>Connection settings for the hosted Foundry KYC agent.</summary>
public class KycAgentOptions
{
    /// <summary>Foundry project endpoint, e.g. https://{service}.services.ai.azure.com/api/projects/{name}.</summary>
    public string ProjectEndpoint { get; set; } = string.Empty;

    /// <summary>Hosted agent name, e.g. hosted-agent-kyc.</summary>
    public string AgentName { get; set; } = "hosted-agent-kyc";

    /// <summary>Entra tenant id override for local development.</summary>
    public string? TenantId { get; set; }

    public bool Enabled => !string.IsNullOrWhiteSpace(ProjectEndpoint);
}

/// <summary>
/// Kicks off the hosted Foundry KYC agent for a customer. The agent works the
/// fixed CIP rulebook and reports every rule result back through this app's
/// /mcp endpoint, so progress lands in the UI while the run is still going.
/// </summary>
public class KycAgentService
{
    private readonly KycCaseService _cases;
    private readonly KycAgentOptions _options;
    private readonly ILogger<KycAgentService> _logger;

    public KycAgentService(KycCaseService cases, IOptions<KycAgentOptions> options, ILogger<KycAgentService> logger)
    {
        _cases = cases;
        _options = options.Value;
        _logger = logger;
    }

    public bool Enabled => _options.Enabled;

    /// <summary>Starts the agent run in the background and returns immediately.</summary>
    public void Kick(KycCase kycCase)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Foundry is not configured; {CustomerId} stays in manual mode.", kycCase.CustomerId);
            _cases.LogActivity(kycCase.CustomerId, new KycActivity
            {
                Step = "Agent run",
                Kind = "flow",
                Message = "Foundry is not configured (KycAgent:ProjectEndpoint is empty); the case stays in manual mode.",
                Status = KycStatus.Blocked,
                Actor = "System"
            });
            return;
        }

        _logger.LogInformation("Starting {AgentName} for {CustomerId}", _options.AgentName, kycCase.CustomerId);
        _cases.LogActivity(kycCase.CustomerId, new KycActivity
        {
            Step = "Agent run",
            Kind = "flow",
            Message = $"Handed off to hosted agent '{_options.AgentName}' to work the {kycCase.Jurisdiction} CIP rulebook.",
            Status = KycStatus.InProgress,
            Actor = "System"
        });

        _ = Task.Run(async () =>
        {
            try
            {
                await RunAsync(kycCase, CancellationToken.None);
                _cases.LogActivity(kycCase.CustomerId, new KycActivity
                {
                    Step = "Agent run",
                    Kind = "flow",
                    Message = "Hosted agent run finished.",
                    Status = KycStatus.Completed,
                    Actor = "Foundry KYC agent"
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "KYC agent run failed for {CustomerId}", kycCase.CustomerId);
                _cases.LogActivity(kycCase.CustomerId, new KycActivity
                {
                    Step = "Agent run",
                    Kind = "flow",
                    Message = $"Agent run failed: {ex.Message}",
                    Status = KycStatus.Blocked,
                    Actor = "Foundry KYC agent"
                });
            }
        });
    }

    private async Task RunAsync(KycCase kycCase, CancellationToken cancellationToken)
    {
        var credentialOptions = new DefaultAzureCredentialOptions();
        if (!string.IsNullOrWhiteSpace(_options.TenantId))
        {
            credentialOptions.TenantId = _options.TenantId;
        }

        // Use the managed identity on App Service; locally use developer
        // credentials and skip the managed identity probe.
        var onAppService = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID"));
        credentialOptions.ExcludeManagedIdentityCredential = !onAppService;
        if (!onAppService)
        {
            // Pin local runs to the `az login` account so a stale Visual Studio
            // or VS Code sign-in cannot be picked up instead.
            credentialOptions.ExcludeVisualStudioCredential = true;
            credentialOptions.ExcludeVisualStudioCodeCredential = true;
            credentialOptions.ExcludeAzurePowerShellCredential = true;
        }

        var clientOptions = new AIProjectClientOptions
        {
            RetryPolicy = new ClientRetryPolicy(maxRetries: 0),
            NetworkTimeout = TimeSpan.FromMinutes(30)
        };
        var projectClient = new AIProjectClient(new Uri(_options.ProjectEndpoint), new DefaultAzureCredential(credentialOptions), clientOptions);
        // The agent endpoint builds its own pipeline, so the no-retry policy has
        // to be repeated here or the SDK default (3 retries) would replay the run.
        var agentClientOptions = new ProjectOpenAIClientOptions
        {
            RetryPolicy = new ClientRetryPolicy(maxRetries: 0),
            NetworkTimeout = TimeSpan.FromMinutes(30)
        };
        // Hosted agents are only reachable through their own agent endpoint.
        var responseClient = projectClient.ProjectOpenAIClient.GetProjectResponsesClientForAgentEndpoint(_options.AgentName, options: agentClientOptions);

        CreateResponseOptions? next = new()
        {
            InputItems = { ResponseItem.CreateUserMessageItem(BuildPrompt(kycCase)) }
        };

        while (next is not null)
        {
            ResponseResult result = await responseClient.CreateResponseAsync(next, cancellationToken);
            next = null;

            // Toolbox MCP calls that request approval are auto-approved: the run
            // is unattended and every tool is a read or a progress callback.
            foreach (var item in result.OutputItems)
            {
                if (item is McpToolCallApprovalRequestItem approval)
                {
                    next ??= new CreateResponseOptions { PreviousResponseId = result.Id };
                    next.InputItems.Add(ResponseItem.CreateMcpApprovalResponseItem(approval.Id, approved: true));
                }
            }
        }
    }

    private static string BuildPrompt(KycCase kycCase) =>
        $"""
        Run the {kycCase.Jurisdiction} KYC/AML CIP check for this customer end to end.

        - customerId: {kycCase.CustomerId}
        - customerName: {kycCase.CustomerName}
        - jurisdiction: {kycCase.Jurisdiction}
        - entityType: {kycCase.EntityType}
        - productScope: {kycCase.ProductScope}
        - businessContact: {kycCase.BusinessContact}
        - regulator: {(string.IsNullOrWhiteSpace(kycCase.Regulator) ? "unknown - research it" : kycCase.Regulator)}
        - listingExchange: {(string.IsNullOrWhiteSpace(kycCase.ListingExchange) ? "unknown - research it" : kycCase.ListingExchange)}

        Work the fixed rulebook from get_cip_rulebook one group at a time, with a single
        bundled search per group, and call submit_group_results once per group, then
        publish the risk assessment, the CIP schedule and the requirement list.
        """;
}
