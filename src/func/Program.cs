using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Onboarding.FunctionApp.Models;
using Onboarding.FunctionApp.Services;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services.Configure<FoundryOptions>(builder.Configuration.GetSection("Foundry"));

builder.Services.AddSingleton<FoundryOnboardingPipelineService>();

builder.Build().Run();
