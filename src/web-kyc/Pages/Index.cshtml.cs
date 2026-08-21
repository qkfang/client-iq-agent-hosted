using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Onboarding.Web.Models;
using Onboarding.Web.Services;

namespace Onboarding.Web.Pages;

public class IndexModel : PageModel
{
    private readonly KycCaseService _cases;

    public IndexModel(KycCaseService cases)
    {
        _cases = cases;
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
            RiskRating = c.RiskAssessment?.RiskRating,
            CipClause = c.CipResult?.ClauseNumber
        })
    });

    public IActionResult OnPostStart(string customerId)
    {
        var existing = _cases.GetCase(customerId);
        _cases.StartCase(customerId, existing?.CustomerName, existing?.Jurisdiction, existing?.EntityType,
            existing?.ProductScope, existing?.BusinessContact, User.Identity?.Name ?? "Analyst");
        return new JsonResult(new { customerId });
    }
}
