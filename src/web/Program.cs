using System.ClientModel.Primitives;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Onboarding.Web.Agents;
using Onboarding.Web.Models;
using Onboarding.Web.Services;
using OpenAI.Responses;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Entra sign-in so the agent can run with the user's token (enabled once an
// AzureAd:ClientId is configured). The user token lets Work IQ resolve the
// signed-in user's Microsoft 365 context via On-Behalf-Of.
var authEnabled = !string.IsNullOrWhiteSpace(builder.Configuration["AzureAd:ClientId"]);
if (authEnabled)
{
    builder.Services
        .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"))
        .EnableTokenAcquisitionToCallDownstreamApi(new[] { FoundryOptions.ResponsesApiScope })
        .AddInMemoryTokenCaches();
    builder.Services.AddControllersWithViews().AddMicrosoftIdentityUI();
}

builder.Services.AddRazorPages(options =>
{
    // Protect the CRM pages but leave the /mcp callback endpoint anonymous.
    if (authEnabled)
    {
        options.Conventions.AuthorizeFolder("/");
    }
});

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

        // Do not retry agent calls; surface failures immediately. Allow up to 10
        // minutes for long-running agent operations before timing out.
        var clientOptions = new AIProjectClientOptions { RetryPolicy = new ClientRetryPolicy(maxRetries: 0), NetworkTimeout = TimeSpan.FromMinutes(10) };
        var projectClient = new AIProjectClient(new Uri(foundry.ProjectEndpoint), new DefaultAzureCredential(credentialOptions), clientOptions);

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

if (authEnabled)
{
    app.UseAuthentication();
}
app.UseAuthorization();

app.UseStaticFiles();
app.MapRazorPages();
if (authEnabled)
{
    app.MapControllers();
}

// MCP endpoint the Foundry onboarding agent calls to update the CRM.
app.MapMcp("/mcp");

app.Run();
