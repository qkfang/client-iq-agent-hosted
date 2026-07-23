using Azure.AI.Projects;
using Azure.Identity;
using Onboarding.Web.Agents;
using Onboarding.Web.Models;
using Onboarding.Web.Services;
using OpenAI.Responses;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

builder.Services.Configure<FoundryOptions>(builder.Configuration.GetSection("Foundry"));
builder.Services.AddSingleton<CrmService>();
builder.Services.AddSingleton<OnboardingAgentService>();

// Register the Foundry onboarding agent and connect all MCP tools at startup.
var foundry = builder.Configuration.GetSection("Foundry").Get<FoundryOptions>() ?? new FoundryOptions();
if (!string.IsNullOrWhiteSpace(foundry.ProjectEndpoint) &&
    !string.IsNullOrWhiteSpace(foundry.SalesCrmOnboardingAgentId) &&
    !string.IsNullOrWhiteSpace(foundry.WebAppMcpUrl))
{
    builder.Services.AddSingleton(sp =>
    {
        var credentialOptions = new DefaultAzureCredentialOptions();
        if (!string.IsNullOrWhiteSpace(foundry.TenantId))
        {
            credentialOptions.TenantId = foundry.TenantId;
        }

        // Use the managed identity on Azure App Service; locally use developer
        // credentials and skip the managed identity probe.
        var onAppService = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID"));
        credentialOptions.ExcludeManagedIdentityCredential = !onAppService;

        var projectClient = new AIProjectClient(new Uri(foundry.ProjectEndpoint), new DefaultAzureCredential(credentialOptions));

        var neverApprove = new McpToolCallApprovalPolicy(GlobalMcpToolCallApprovalPolicy.NeverRequireApproval);
        var tools = new List<ResponseTool>();

        // This web app's own MCP endpoint used to finalise the CRM record.
        tools.Add(ResponseTool.CreateMcpTool(
            serverLabel: "onboarding-crm-mcp",
            serverUri: new Uri(foundry.WebAppMcpUrl),
            toolCallApprovalPolicy: neverApprove));

        // Work IQ, Foundry IQ and Fabric IQ MCP tools reached via Foundry project
        // connections. The connection id is set through the tool's JSON patch.
        foreach (var tool in foundry.ConnectedTools)
        {
            var mcpTool = ResponseTool.CreateMcpTool(
                serverLabel: tool.ServerLabel,
                serverUri: new Uri(tool.ServerUrl),
                toolCallApprovalPolicy: neverApprove);
            mcpTool.Patch.Set("$.project_connection_id"u8, tool.ProjectConnectionId);
            tools.Add(mcpTool);
        }

        // Bing web search used to research the customer.
        if (foundry.EnableWebSearch)
        {
            tools.Add(ResponseTool.CreateWebSearchTool());
        }

        return new OnboardingAgent(
            projectClient,
            foundry.SalesCrmOnboardingAgentId,
            foundry.ModelDeploymentName,
            tools,
            sp.GetRequiredService<ILogger<OnboardingAgent>>());
    });
}

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

// Create the onboarding agent and register its agent version on startup.
app.Services.GetService<OnboardingAgent>();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.UseStaticFiles();
app.MapRazorPages();

// MCP endpoint the Foundry onboarding agent calls to update the CRM.
app.MapMcp("/mcp");

app.Run();
