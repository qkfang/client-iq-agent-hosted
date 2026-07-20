using Azure.AI.Agents.Persistent;
using Azure.Identity;
using Microsoft.Extensions.Options;
using Onboarding.Web.Models;

namespace Onboarding.Web.Services;

/// <summary>
/// Invokes the Foundry SalesCRMOnboarding agent for a queued candidate. The
/// agent researches the company and calls back through the /mcp endpoint to
/// finalise the CRM record. When Foundry is not configured, the candidate is
/// onboarded locally so the demo remains functional.
/// </summary>
public class OnboardingAgentService
{
    private readonly CrmService _crmService;
    private readonly FoundryOptions _options;
    private readonly ILogger<OnboardingAgentService> _logger;
    private readonly PersistentAgentsClient? _client;

    public OnboardingAgentService(CrmService crmService, IOptions<FoundryOptions> options, ILogger<OnboardingAgentService> logger)
    {
        _crmService = crmService;
        _options = options.Value;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_options.ProjectEndpoint))
        {
            var credentialOptions = new DefaultAzureCredentialOptions();
            if (!string.IsNullOrWhiteSpace(_options.TenantId))
            {
                credentialOptions.TenantId = _options.TenantId;
            }

            _client = new PersistentAgentsClient(_options.ProjectEndpoint, new DefaultAzureCredential(credentialOptions));
        }
    }

    public async Task<string> OnboardAsync(OnboardingCandidate candidate, CancellationToken cancellationToken = default)
    {
        _crmService.SetCandidateStatus(candidate.CandidateId, "Onboarding in progress");

        if (_client is null || string.IsNullOrWhiteSpace(_options.SalesCrmOnboardingAgentId))
        {
            _crmService.FinalizeOnboarding(candidate.CandidateId, BuildLocalRecord(candidate));
            return "Foundry not configured. Candidate onboarded locally.";
        }

        var prompt =
            $"Onboard the following prospective customer (candidateId: {candidate.CandidateId}). " +
            "Research the company, assess KYC risk, then call the finalize_customer_onboarding MCP tool " +
            "with the completed details to create the CRM record.\n\n" +
            $"Company: {candidate.CompanyName}\n" +
            $"Legal entity type: {candidate.LegalEntityType}\n" +
            $"Country: {candidate.Country}\n" +
            $"Industry: {candidate.Industry}\n" +
            $"Contact: {candidate.ContactName} ({candidate.ContactEmail})\n" +
            $"Website: {candidate.Website}";

        var thread = await _client.Threads.CreateThreadAsync(cancellationToken: cancellationToken);
        await _client.Messages.CreateMessageAsync(thread.Value.Id, MessageRole.User, prompt, cancellationToken: cancellationToken);

        ThreadRun run = await _client.Runs.CreateRunAsync(thread.Value.Id, _options.SalesCrmOnboardingAgentId, cancellationToken: cancellationToken);
        while (run.Status == RunStatus.Queued || run.Status == RunStatus.InProgress)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            run = await _client.Runs.GetRunAsync(thread.Value.Id, run.Id, cancellationToken);
        }

        if (run.Status != RunStatus.Completed)
        {
            _logger.LogWarning("Onboarding agent run finished with status {Status} for {CandidateId}", run.Status, candidate.CandidateId);
            _crmService.SetCandidateStatus(candidate.CandidateId, "Onboarding failed");
            return $"Onboarding agent did not complete (status: {run.Status}).";
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
                return textContent.Text;
            }
        }

        return "Onboarding agent completed.";
    }

    private static CrmRecord BuildLocalRecord(OnboardingCandidate candidate) => new()
    {
        CustomerName = candidate.CompanyName,
        LegalEntityType = candidate.LegalEntityType,
        Country = candidate.Country,
        Industry = candidate.Industry,
        PrimaryContactName = candidate.ContactName,
        PrimaryContactEmail = candidate.ContactEmail,
        Website = candidate.Website,
        OnboardingStatus = "Ready to trade",
        KycRiskRating = "Low",
        LastUpdatedBy = "SalesCRMOnboarding agent",
        Notes = "Onboarded from candidate queue."
    };
}
