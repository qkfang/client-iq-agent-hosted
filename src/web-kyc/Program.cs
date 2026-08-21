using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Onboarding.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationInsightsTelemetry();

// Entra sign-in is enabled once an AzureAd:ClientId is configured.
var authEnabled = !string.IsNullOrWhiteSpace(builder.Configuration["AzureAd:ClientId"]);
if (authEnabled)
{
    builder.Services
        .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApp(options =>
        {
            builder.Configuration.GetSection("AzureAd").Bind(options);
            // Authorization code flow; implicit id_token is disabled on the app registration.
            options.ResponseType = OpenIdConnectResponseType.Code;
        });
    builder.Services.AddControllersWithViews().AddMicrosoftIdentityUI();
}

builder.Services.AddRazorPages(options =>
{
    // Protect the tracking pages but leave the /mcp endpoint anonymous.
    if (authEnabled)
    {
        options.Conventions.AuthorizeFolder("/");
    }
});

builder.Services.AddSingleton<KycCaseService>();
builder.Services.Configure<KycAgentOptions>(builder.Configuration.GetSection("KycAgent"));
builder.Services.AddSingleton<KycAgentService>();

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
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

// MCP endpoint the Foundry KYC agent calls to push case progress.
app.MapMcp("/mcp");

app.Run();
