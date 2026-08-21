using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Onboarding.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationInsightsTelemetry();

// Entra sign-in is enabled once an AzureAd:ClientId is configured.
var authEnabled = !string.IsNullOrWhiteSpace(builder.Configuration["AzureAd:ClientId"]);
if (authEnabled)
{
    builder.Services
        .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));
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
