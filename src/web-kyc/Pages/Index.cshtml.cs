using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Onboarding.Web.Models;
using Onboarding.Web.Services;

namespace Onboarding.Web.Pages;

public class IndexModel : PageModel
{
    private readonly KycCaseService _cases;
    private readonly KycAgentService _agent;

    public IndexModel(KycCaseService cases, KycAgentService agent)
    {
        _cases = cases;
        _agent = agent;
    }

    public IActionResult OnGetFeed() => new JsonResult(new
    {
        version = _cases.CurrentVersion,
        customers = _cases.GetCases().Select(c => new
        {
            c.CustomerId,
            c.CustomerName,
            c.Jurisdiction,
            c.EntityType,
            c.ProductScope,
            c.BusinessContact,
            c.CaseStatus,
            c.CurrentStage,
            c.NextStepsRequired,
            c.ActionableBy,
            c.ReadinessPercent,
            c.LastUpdatedBy,
            c.LastUpdatedUtc,
            OpenRequirements = c.Requirements.Count(r => r.Status == KycStatus.Outstanding),
            RulesChecked = c.PolicyChecks.Count == 0 ? null : $"{c.PolicyChecksCleared} / {c.PolicyChecks.Count}",
            RiskRating = c.RiskAssessment?.RiskRating,
            CipClause = c.CipResult?.ClauseNumber
        })
    });

    public IActionResult OnPostStart(string customerId)
    {
        var existing = _cases.GetCase(customerId);
        var kycCase = _cases.StartCase(customerId, existing?.CustomerName, existing?.Jurisdiction, existing?.EntityType,
            existing?.ProductScope, existing?.BusinessContact, User.Identity?.Name ?? "Analyst");
        _agent.Kick(kycCase);
        return new JsonResult(new { customerId });
    }
}
