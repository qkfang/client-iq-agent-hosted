using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Onboarding.Web.Services;

namespace Onboarding.Web.Pages;

public class CaseModel : PageModel
{
    private readonly KycCaseService _cases;
    private readonly KycAgentService _agent;

    public CaseModel(KycCaseService cases, KycAgentService agent)
    {
        _cases = cases;
        _agent = agent;
    }

    public string CustomerId { get; private set; } = string.Empty;
    public string CustomerName { get; private set; } = string.Empty;

    private string Reviewer => User.Identity?.Name ?? "Analyst";

    public IActionResult OnGet(string customerId)
    {
        var kycCase = _cases.GetCase(customerId);
        if (kycCase is null)
        {
            return RedirectToPage("/Index");
        }

        CustomerId = kycCase.CustomerId;
        CustomerName = kycCase.CustomerName;
        return Page();
    }

    public IActionResult OnGetFeed(string customerId)
    {
        var kycCase = _cases.GetCase(customerId);
        return kycCase is null ? NotFound() : new JsonResult(kycCase);
    }

    public IActionResult OnPostStart(string customerId)
    {
        var existing = _cases.GetCase(customerId);
        var kycCase = _cases.StartCase(customerId, existing?.CustomerName, existing?.Jurisdiction,
            existing?.EntityType, existing?.ProductScope, existing?.BusinessContact, Reviewer);
        _agent.Kick(kycCase);
        return new JsonResult(kycCase);
    }

    public IActionResult OnPostApprove(string customerId, string target, string state)
    {
        var kycCase = _cases.SetApproval(customerId, target, state, Reviewer);
        return kycCase is null ? NotFound() : new JsonResult(kycCase);
    }

    public IActionResult OnPostRequirement(string customerId, int requirementId, string status)
    {
        var kycCase = _cases.UpdateRequirement(customerId, requirementId, status, null, null, Reviewer);
        return kycCase is null ? NotFound() : new JsonResult(kycCase);
    }
}
