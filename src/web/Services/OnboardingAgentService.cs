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

    public OnboardingAgentService(CrmService crmService, ILogger<OnboardingAgentService> logger, OnboardingAgent? agent = null)
    {
        _crmService = crmService;
        _logger = logger;
        _agent = agent;
    }

    public async Task<string> OnboardAsync(OnboardingCandidate candidate, CancellationToken cancellationToken = default)
    {
        _crmService.SetCandidateStatus(candidate.CandidateId, "Onboarding in progress");

        if (_agent is null)
        {
            _crmService.FinalizeOnboarding(candidate.CandidateId, BuildLocalRecord(candidate));
            return "Foundry not configured. Candidate onboarded locally.";
        }

        try
        {
            var output = await _agent.RunAsync(BuildPrompt(candidate), cancellationToken);

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

    private static string BuildPrompt(OnboardingCandidate candidate) =>
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
