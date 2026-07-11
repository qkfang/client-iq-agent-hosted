using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Onboarding.FunctionApp.Services;

namespace Onboarding.FunctionApp.Functions;

/// <summary>
/// Watches the onboarding-forms blob container for new customer onboarding
/// forms and runs each one through the Foundry onboarding agent pipeline
/// (intake → orchestrator → sub agent).
/// </summary>
public class OnboardingFormBlobTrigger
{
    private readonly FoundryOnboardingPipelineService _pipelineService;
    private readonly ILogger<OnboardingFormBlobTrigger> _logger;

    public OnboardingFormBlobTrigger(FoundryOnboardingPipelineService pipelineService, ILogger<OnboardingFormBlobTrigger> logger)
    {
        _pipelineService = pipelineService;
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

        var result = await _pipelineService.ProcessOnboardingFormAsync(name, formContent);

        _logger.LogInformation("Onboarding pipeline result for {FileName}: route={Route}, response={Response}", result.FileName, result.Route, result.AgentResponse);
    }
}
