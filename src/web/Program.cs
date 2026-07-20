using Onboarding.Web.Models;
using Onboarding.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

builder.Services.Configure<FoundryOptions>(builder.Configuration.GetSection("Foundry"));
builder.Services.AddSingleton<CrmService>();
builder.Services.AddSingleton<OnboardingAgentService>();

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

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
