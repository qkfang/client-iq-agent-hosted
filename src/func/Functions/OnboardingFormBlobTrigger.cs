using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Onboarding.FunctionApp.Services;

namespace Onboarding.FunctionApp.Functions;

/// <summary>
/// Watches the onboarding-forms blob container for new customer onboarding
/// forms and hands each one to the Foundry onboarding agent for processing.
/// </summary>
public class OnboardingFormBlobTrigger
{
    private readonly FoundryOnboardingAgentService _agentService;
    private readonly ILogger<OnboardingFormBlobTrigger> _logger;

    public OnboardingFormBlobTrigger(FoundryOnboardingAgentService agentService, ILogger<OnboardingFormBlobTrigger> logger)
    {
        _agentService = agentService;
        _logger = logger;
    }

    [Function("OnboardingFormBlobTrigger")]
    public async Task Run(
        [BlobTrigger("%ONBOARDING_CONTAINER_NAME%/{name}", Connection = "AzureWebJobsStorage")] Stream formStream,
        string name)
    {
        _logger.LogInformation("New onboarding form detected: {FileName}", name);

        using var reader = new StreamReader(formStream, Encoding.UTF8);
        var formContent = await reader.ReadToEndAsync();

        var result = await _agentService.ProcessOnboardingFormAsync(name, formContent);

        _logger.LogInformation("Onboarding agent response for {FileName}: {Response}", result.FileName, result.AgentResponse);
    }
}
