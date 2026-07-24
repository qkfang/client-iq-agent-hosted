using System.ClientModel.Primitives;
using Azure.AI.Projects;
using Azure.Core;
using Microsoft.Extensions.Options;
using Onboarding.Web.Agents;
using Onboarding.Web.Models;

namespace Onboarding.Web.Services;

/// <summary>
/// Invokes the Foundry onboarding agent for a queued candidate. The agent is
/// created and wired to all MCP tools at startup (see Program.cs); this service
/// builds the prompt and runs it. When Foundry is not configured, the candidate
/// is onboarded locally so the demo remains functional.
/// </summary>
public class OnboardingAgentService
{
    private readonly CrmService _crmService;
    private readonly ILogger<OnboardingAgentService> _logger;
    private readonly OnboardingAgent? _agent;
    private readonly FoundryOptions _foundry;

    public OnboardingAgentService(CrmService crmService, ILogger<OnboardingAgentService> logger, IOptions<FoundryOptions> foundry, OnboardingAgent? agent = null)
    {
        _crmService = crmService;
        _logger = logger;
        _foundry = foundry.Value;
        _agent = agent;
    }

    /// <summary>
    /// Onboards a candidate. When <paramref name="userCredential"/> is supplied,
    /// the agent runs with the signed-in user's token so Work IQ resolves that
    /// user's Microsoft 365 context; otherwise it runs with the app identity.
    /// A caller may supply an edited <paramref name="message"/> to override the
    /// prompt built from the candidate.
    /// </summary>
    public async Task<string> OnboardAsync(OnboardingCandidate candidate, string? message = null, TokenCredential? userCredential = null, CancellationToken cancellationToken = default)
    {
        _crmService.SetCandidateStatus(candidate.CandidateId, "Onboarding in progress");

        if (_agent is null)
        {
            _crmService.FinalizeOnboarding(candidate.CandidateId, BuildLocalRecord(candidate));
            return "Foundry not configured. Candidate onboarded locally.";
        }

        try
        {
            var prompt = string.IsNullOrWhiteSpace(message) ? BuildPrompt(candidate) : message;
            var output = userCredential is not null
                ? await _agent.RunAsync(BuildUserClient(userCredential), prompt, cancellationToken)
                : await _agent.RunAsync(prompt, cancellationToken);

            // The agent finalises the record by calling finalize_customer_onboarding
            // on this app's /mcp endpoint. When that callback lands on a different
            // instance (local development, or a scaled-out App Service where the
            // in-memory CRM is not shared) this process won't observe the update, so
            // finalise locally to keep the CRM view consistent and move the candidate
            // into the Customers tab.
            if (_crmService.GetOnboardingCandidate(candidate.CandidateId)?.Status != "Onboarded")
            {
                _crmService.FinalizeOnboarding(candidate.CandidateId, BuildLocalRecord(candidate));
            }

            return output;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Onboarding agent failed for {CandidateId}", candidate.CandidateId);
            _crmService.SetCandidateStatus(candidate.CandidateId, "Onboarding failed");
            return $"Onboarding agent failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Runs an arbitrary prompt through the agent without finalising a candidate.
    /// Used by the free-form onboarding prompt box.
    /// </summary>
    public async Task<string> RunPromptAsync(string message, TokenCredential? userCredential = null, CancellationToken cancellationToken = default)
    {
        if (_agent is null)
        {
            return "Foundry not configured. Agent is unavailable.";
        }

        try
        {
            return userCredential is not null
                ? await _agent.RunAsync(BuildUserClient(userCredential), message, cancellationToken)
                : await _agent.RunAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Agent prompt run failed");
            return $"Agent failed: {ex.Message}";
        }
    }

    private AIProjectClient BuildUserClient(TokenCredential userCredential) =>
        new(new Uri(_foundry.ProjectEndpoint), userCredential, new AIProjectClientOptions { RetryPolicy = new ClientRetryPolicy(maxRetries: 0), NetworkTimeout = TimeSpan.FromMinutes(10) });

    /// <summary>Builds the default onboarding prompt for a candidate.</summary>
    public static string BuildPrompt(OnboardingCandidate candidate) =>
        "Onboard the following prospective customer from this onboarding form.\n\n" +
        "# Customer Onboarding Form\n\n" +
        $"- candidateId: {candidate.CandidateId}\n" +
        $"- Company: {candidate.CompanyName}\n" +
        $"- Legal entity type: {candidate.LegalEntityType}\n" +
        $"- Country: {candidate.Country}\n" +
        $"- Industry: {candidate.Industry}\n" +
        $"- Contact: {candidate.ContactName} ({candidate.ContactEmail})\n" +
        $"- Website: {candidate.Website}";

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
