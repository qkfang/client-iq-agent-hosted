using Azure.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Identity.Client;
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

    public async Task<IActionResult> OnGetAsync()
    {
        // On open, confirm the signed-in user's token is still usable for the
        // agent session. The auth cookie survives a restart but the in-memory
        // MSAL cache does not, so silently re-authenticate to repopulate it
        // before the user starts onboarding.
        var tokenAcquisition = HttpContext.RequestServices.GetService<ITokenAcquisition>();
        if (tokenAcquisition is not null)
        {
            try
            {
                await tokenAcquisition.GetAuthenticationResultForUserAsync(
                    new[] { FoundryOptions.ResponsesApiScope });
            }
            catch (Exception ex) when (ex is MicrosoftIdentityWebChallengeUserException or MsalUiRequiredException)
            {
                return Challenge();
            }
        }

        Candidates = _crmService.GetOnboardingCandidates();
        return Page();
    }

    public IActionResult OnGetMessage(string candidateId)
    {
        var candidate = _crmService.GetOnboardingCandidate(candidateId);
        if (candidate is null)
        {
            return new JsonResult(new { success = false, message = "Candidate not found." });
        }

        return new JsonResult(new { success = true, message = OnboardingAgentService.BuildPrompt(candidate) });
    }

    public async Task<IActionResult> OnPostOnboardAsync(string? candidateId, string message, CancellationToken cancellationToken)
    {
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
            catch (Exception ex) when (ex is MicrosoftIdentityWebChallengeUserException or MsalUiRequiredException)
            {
                // The token cache was lost (e.g. app restart). Ask the client to
                // reload so the page can silently re-authenticate the user.
                return new JsonResult(new { success = false, reauth = true, message = "Your session expired. Reloading to sign you back in." });
            }
        }

        // Free-form prompt: no candidate attached, just run the agent.
        if (string.IsNullOrWhiteSpace(candidateId))
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return new JsonResult(new { success = false, message = "Enter a prompt to send to the agent." });
            }

            var output = await _onboardingAgent.RunPromptAsync(message, userCredential, cancellationToken);
            return new JsonResult(new { success = true, message = output });
        }

        var candidate = _crmService.GetOnboardingCandidate(candidateId);
        if (candidate is null)
        {
            return new JsonResult(new { success = false, message = "Candidate not found." });
        }

        var agentOutput = await _onboardingAgent.OnboardAsync(candidate, message, userCredential, cancellationToken);
        var updated = _crmService.GetOnboardingCandidate(candidateId);
        var success = string.Equals(updated?.Status, "Onboarded", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(updated!.CreatedCustomerId);

        return new JsonResult(new
        {
            success,
            message = agentOutput,
            candidateId,
            companyName = candidate.CompanyName,
            customerId = updated?.CreatedCustomerId
        });
    }
}
