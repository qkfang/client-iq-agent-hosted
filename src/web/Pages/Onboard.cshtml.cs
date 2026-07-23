using Azure.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Identity.Web;
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

        // Acquire the signed-in user's token so the agent (and Work IQ) run in
        // the user's context. Falls back to the app identity when sign-in is off.
        TokenCredential? userCredential = null;
        var tokenAcquisition = HttpContext.RequestServices.GetService<ITokenAcquisition>();
        if (tokenAcquisition is not null)
        {
            try
            {
                var result = await tokenAcquisition.GetAuthenticationResultForUserAsync(
                    new[] { FoundryOptions.ResponsesApiScope });
                userCredential = new StaticTokenCredential(result.AccessToken, result.ExpiresOn);
            }
            catch (MicrosoftIdentityWebChallengeUserException)
            {
                return Challenge();
            }
        }

        var message = await _onboardingAgent.OnboardAsync(candidate, userCredential, cancellationToken);
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
