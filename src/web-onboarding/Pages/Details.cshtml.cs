using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Onboarding.Web.Models;
using Onboarding.Web.Services;

namespace Onboarding.Web.Pages;

public class DetailsModel : PageModel
{
    private readonly CrmService _crmService;

    public DetailsModel(CrmService crmService)
    {
        _crmService = crmService;
    }

    public CrmRecord? Customer { get; private set; }
    public IReadOnlyCollection<Organization> Organizations { get; private set; } = Array.Empty<Organization>();
    public IReadOnlyCollection<HistoryEntry> History { get; private set; } = Array.Empty<HistoryEntry>();
    public IReadOnlyCollection<EmailMessage> Emails { get; private set; } = Array.Empty<EmailMessage>();
    public IReadOnlyCollection<Opportunity> Opportunities { get; private set; } = Array.Empty<Opportunity>();

    public IActionResult OnGet(string customerId)
    {
        Customer = _crmService.GetCustomer(customerId);
        if (Customer is null)
        {
            return NotFound();
        }

        Organizations = _crmService.GetOrganizations(customerId);
        History = _crmService.GetHistory(customerId);
        Emails = _crmService.GetEmails(customerId);
        Opportunities = _crmService.GetOpportunities(customerId);
        return Page();
    }
}
