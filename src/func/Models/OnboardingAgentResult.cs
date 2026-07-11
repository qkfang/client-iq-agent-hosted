namespace Onboarding.FunctionApp.Models;

/// <summary>
/// Result returned by the onboarding agent after processing an onboarding form.
/// </summary>
public sealed record OnboardingAgentResult(string FileName, string AgentResponse);
