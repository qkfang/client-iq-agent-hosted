using Microsoft.AspNetCore.Mvc.RazorPages;
using Onboarding.Web.Models;
using Onboarding.Web.Services;

namespace Onboarding.Web.Pages;

public class IndexModel : PageModel
{
    private readonly CrmService _crmService;

    public IndexModel(CrmService crmService)
    {
        _crmService = crmService;
    }

    public IReadOnlyCollection<CrmRecord> Records { get; private set; } = Array.Empty<CrmRecord>();

    public void OnGet()
    {
        Records = _crmService.GetAll();
    }
}
