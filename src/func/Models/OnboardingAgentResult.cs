namespace Onboarding.FunctionApp.Models;

/// <summary>
/// Result returned by the onboarding pipeline after processing an onboarding form.
/// </summary>
public sealed record OnboardingAgentResult(string FileName, string Route, string AgentResponse);
