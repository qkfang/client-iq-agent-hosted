using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Microsoft.Extensions.Options;
using Onboarding.Web.Models;
using OpenAI.Responses;

namespace Onboarding.Web.Services;

/// <summary>
/// Invokes the Foundry SalesCRMOnboarding agent for a queued candidate using
/// the Azure AI Foundry Responses API. The agent researches the company and
/// calls back through this web app's /mcp endpoint to finalise the CRM record.
/// When Foundry is not configured, the candidate is onboarded locally so the
/// demo remains functional.
/// </summary>
public class OnboardingAgentService
{
    private const string McpServerLabel = "onboarding-crm-mcp";

    private const string Instructions =
        "You are the SalesCRMOnboarding agent. You receive a prospective customer to onboard, " +
        "including its candidateId and basic attributes. Research the company, assess a KYC/AML " +
        "risk rating (Low, Medium or High), then call the finalize_customer_onboarding MCP tool " +
        "with the completed details to create the CRM record. Always pass the candidateId through " +
        "unchanged and set a final onboarding status of 'Ready to trade'.";

    private readonly CrmService _crmService;
    private readonly FoundryOptions _options;
    private readonly ILogger<OnboardingAgentService> _logger;
    private readonly AIProjectClient? _projectClient;

    public OnboardingAgentService(CrmService crmService, IOptions<FoundryOptions> options, ILogger<OnboardingAgentService> logger)
    {
        _crmService = crmService;
        _options = options.Value;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_options.ProjectEndpoint))
        {
            var credentialOptions = new Azure.Identity.DefaultAzureCredentialOptions();
            if (!string.IsNullOrWhiteSpace(_options.TenantId))
            {
                credentialOptions.TenantId = _options.TenantId;
            }

            // On Azure App Service use the managed identity; locally fall back to
            // developer credentials (Azure CLI / Visual Studio) and skip the slow
            // IMDS probe that fails on machines without a managed identity.
            var onAppService = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID"));
            credentialOptions.ExcludeManagedIdentityCredential = !onAppService;

            _projectClient = new AIProjectClient(new Uri(_options.ProjectEndpoint), new Azure.Identity.DefaultAzureCredential(credentialOptions));
        }
    }

    public async Task<string> OnboardAsync(OnboardingCandidate candidate, CancellationToken cancellationToken = default)
    {
        _crmService.SetCandidateStatus(candidate.CandidateId, "Onboarding in progress");

        if (_projectClient is null ||
            string.IsNullOrWhiteSpace(_options.SalesCrmOnboardingAgentId) ||
            string.IsNullOrWhiteSpace(_options.WebAppMcpUrl))
        {
            _crmService.FinalizeOnboarding(candidate.CandidateId, BuildLocalRecord(candidate));
            return "Foundry not configured. Candidate onboarded locally.";
        }

        try
        {
            var output = await RunAgentAsync(BuildPrompt(candidate), cancellationToken);

            if (_crmService.GetOnboardingCandidate(candidate.CandidateId)?.Status != "Onboarded")
            {
                _crmService.SetCandidateStatus(candidate.CandidateId, "Awaiting agent");
            }

            return output;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Onboarding agent failed for {CandidateId}", candidate.CandidateId);
            _crmService.SetCandidateStatus(candidate.CandidateId, "Onboarding failed");
            return $"Onboarding agent failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Creates (or refreshes) the agent version wired to this app's MCP tool,
    /// runs the prompt through it, auto-approves any MCP tool calls, and
    /// returns the final text output.
    /// </summary>
    private async Task<string> RunAgentAsync(string prompt, CancellationToken cancellationToken)
    {
        var mcpTool = ResponseTool.CreateMcpTool(
            serverLabel: McpServerLabel,
            serverUri: new Uri(_options.WebAppMcpUrl),
            toolCallApprovalPolicy: new McpToolCallApprovalPolicy(GlobalMcpToolCallApprovalPolicy.NeverRequireApproval));

        var definition = new DeclarativeAgentDefinition(model: _options.ModelDeploymentName)
        {
            Instructions = Instructions,
        };
        definition.Tools.Add(mcpTool);

        var version = await _projectClient!.AgentAdministrationClient.CreateAgentVersionAsync(
            _options.SalesCrmOnboardingAgentId,
            new ProjectsAgentVersionCreationOptions(definition));

        var responseClient = _projectClient.ProjectOpenAIClient.GetProjectResponsesClientForAgent(version.Value.Name);

        CreateResponseOptions? nextOptions = new()
        {
            InputItems = { ResponseItem.CreateUserMessageItem(prompt) },
        };

        ResponseResult? result = null;
        while (nextOptions is not null)
        {
            result = await responseClient.CreateResponseAsync(nextOptions, cancellationToken);
            nextOptions = null;

            foreach (var item in result.OutputItems)
            {
                if (item is McpToolCallApprovalRequestItem mcpCall)
                {
                    nextOptions ??= new CreateResponseOptions { PreviousResponseId = result.Id };
                    nextOptions.InputItems.Add(ResponseItem.CreateMcpApprovalResponseItem(mcpCall.Id, approved: true));
                }
            }
        }

        return result?.GetOutputText() ?? "Onboarding agent completed.";
    }

    private static string BuildPrompt(OnboardingCandidate candidate) =>
        "Onboard the following prospective customer.\n\n" +
        $"candidateId: {candidate.CandidateId}\n" +
        $"Company: {candidate.CompanyName}\n" +
        $"Legal entity type: {candidate.LegalEntityType}\n" +
        $"Country: {candidate.Country}\n" +
        $"Industry: {candidate.Industry}\n" +
        $"Contact: {candidate.ContactName} ({candidate.ContactEmail})\n" +
        $"Website: {candidate.Website}";

    private static CrmRecord BuildLocalRecord(OnboardingCandidate candidate) => new()
    {
        CustomerName = candidate.CompanyName,
        LegalEntityType = candidate.LegalEntityType,
        Country = candidate.Country,
        Industry = candidate.Industry,
        PrimaryContactName = candidate.ContactName,
        PrimaryContactEmail = candidate.ContactEmail,
        Website = candidate.Website,
        OnboardingStatus = "Ready to trade",
        KycRiskRating = "Low",
        LastUpdatedBy = "SalesCRMOnboarding agent",
        Notes = "Onboarded from candidate queue."
    };
}
