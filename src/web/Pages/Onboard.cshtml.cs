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

    [TempData]
    public string? Message { get; set; }

    public void OnGet()
    {
        Candidates = _crmService.GetOnboardingCandidates();
    }

    public async Task<IActionResult> OnPostOnboardAsync(string candidateId, CancellationToken cancellationToken)
    {
        var candidate = _crmService.GetOnboardingCandidate(candidateId);
        if (candidate is null)
        {
            return NotFound();
        }

        Message = await _onboardingAgent.OnboardAsync(candidate, cancellationToken);
        return RedirectToPage();
    }
}
