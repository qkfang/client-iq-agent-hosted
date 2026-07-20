using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Onboarding.Web.Models;
using Onboarding.Web.Services;

namespace Onboarding.Web.Pages;

public class OnboardModel : PageModel
{
    private readonly CrmService _crmService;
    private readonly OnboardingAgentService _onboardingAgent;

    public OnboardModel(CrmService crmService, OnboardingAgentService onboardingAgent)
    {
        _crmService = crmService;
        _onboardingAgent = onboardingAgent;
    }

    public IReadOnlyCollection<OnboardingCandidate> Candidates { get; private set; } = Array.Empty<OnboardingCandidate>();

    public void OnGet()
    {
        Candidates = _crmService.GetOnboardingCandidates();
    }

    public async Task<IActionResult> OnPostOnboardAsync(string candidateId, CancellationToken cancellationToken)
    {
        var candidate = _crmService.GetOnboardingCandidate(candidateId);
        if (candidate is null)
        {
            return new JsonResult(new { success = false, message = "Candidate not found." });
        }

        var message = await _onboardingAgent.OnboardAsync(candidate, cancellationToken);
        var updated = _crmService.GetOnboardingCandidate(candidateId);
        var success = string.Equals(updated?.Status, "Onboarded", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(updated!.CreatedCustomerId);

        return new JsonResult(new
        {
            success,
            message,
            candidateId,
            companyName = candidate.CompanyName,
            customerId = updated?.CreatedCustomerId
        });
    }
}
