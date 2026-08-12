using System.ClientModel.Primitives;
using System.Text.Json;
using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.Identity;
using OpenAI.Responses;

// Minimal console walkthrough of Foundry tool authentication, following
// https://netweblog.wordpress.com/2026/04/27/foundry-tool-authentication-with-code/
//
// Key idea from the article: Foundry MCP tools have BUILT-IN authentication.
// The client connects with its own project/developer identity and Foundry
// handles the tool token server-side. The only case the client must handle is
// "OAuth Identity Passthrough", where Foundry returns a consent link, the user
// logs in, and the client re-runs the same prompt in the same session.

// ---- Hard-coded settings (GitHub MCP via OAuth identity passthrough) --------
const string ProjectEndpoint = "https://aisa-ciquocsj.services.ai.azure.com/api/projects/aifp-ciquocsj";
const string AgentId = "oauth-test-agent1";
const string ModelDeploymentName = "gpt-5.6-sol";
const string McpServerLabel = "github";
const string McpServerUrl = "https://api.githubcopilot.com/mcp/";
const string ProjectConnectionId = "oauth-passthrough-conn";
const string Prompt = "What is my username in my GitHub profile?";

// ============================================================================
// STEP 1 - Connect with the PROJECT/DEVELOPER identity (not a user token).
// AzureCliCredential uses your `az login` locally; on Azure this would be the
// Foundry project managed identity. This identity only needs RBAC on the
// project - the MCP tool's own auth is handled by Foundry.
// ============================================================================
var credential = new AzureCliCredential();
var projectClient = new AIProjectClient(new Uri(ProjectEndpoint), credential);
Console.WriteLine("[Step 1] Connected to Foundry with the project identity");

// ============================================================================
// STEP 2 - Create an agent version wired to an MCP tool configured for OAuth
// Identity Passthrough (server_label + project_connection_id, never approve).
// ============================================================================
var mcpTool = ResponseTool.CreateMcpTool(
    serverLabel: McpServerLabel,
    serverUri: new Uri(McpServerUrl),
    toolCallApprovalPolicy: new McpToolCallApprovalPolicy(GlobalMcpToolCallApprovalPolicy.NeverRequireApproval));
mcpTool.Patch.Set("$.project_connection_id"u8, ProjectConnectionId);

var definition = new DeclarativeAgentDefinition(model: ModelDeploymentName)
{
    Instructions = "You are a helpful assistant that uses the provided MCP tool.",
};
definition.Tools.Add(mcpTool);

var version = projectClient.AgentAdministrationClient.CreateAgentVersion(
    AgentId,
    new ProjectsAgentVersionCreationOptions(definition)).Value;

var responseClient = projectClient.ProjectOpenAIClient.GetProjectResponsesClientForAgent(version.Name);
Console.WriteLine($"[Step 2] Created agent version {version.Name}");

// ============================================================================
// STEP 3 - First run, STREAMED. OAuth identity passthrough surfaces the consent
// link as an `oauth_consent_request` output item mid-stream. A non-streamed call
// instead fails with HTTP 424 (tool_server_error), so we must stream here.
// ============================================================================
var firstOptions = new CreateResponseOptions
{
    InputItems = { ResponseItem.CreateUserMessageItem(Prompt) },
    ToolChoice = ResponseToolChoice.CreateRequiredChoice(),
};
var first = await RunStreamAsync(firstOptions);

// ============================================================================
// STEP 4 - If consent is required, show the login link(s) and wait for the user
// to sign in, then re-run the SAME prompt with PreviousResponseId so the session
// inherits the token Foundry acquired during login.
// ============================================================================
var output = first.Output;
if (first.ConsentLinks.Count > 0)
{
    Console.WriteLine("[Step 4] Sign in to authorize the tool, then press Enter:");
    foreach (var link in first.ConsentLinks)
    {
        Console.WriteLine($"  {link}");
    }
    Console.ReadLine();

    var retryOptions = new CreateResponseOptions
    {
        PreviousResponseId = first.ResponseId,
        InputItems = { ResponseItem.CreateUserMessageItem(Prompt) },
        ToolChoice = ResponseToolChoice.CreateRequiredChoice(),
    };
    output = (await RunStreamAsync(retryOptions)).Output;
}
else
{
    Console.WriteLine("[Step 4] No consent required (built-in tool auth handled server-side)");
}

// ============================================================================
// STEP 5 - Print the final agent output.
// ============================================================================
Console.WriteLine("[Step 5] Agent output:");
Console.WriteLine(output);

// Streams one response; collects consent links, the response id, and the text.
async Task<(string? ResponseId, List<string> ConsentLinks, string Output)> RunStreamAsync(CreateResponseOptions options)
{
    var consentLinks = new List<string>();
    string? responseId = null;
    var text = string.Empty;

    await foreach (StreamingResponseUpdate update in responseClient.CreateResponseStreamingAsync(options))
    {
        switch (update)
        {
            case StreamingResponseOutputItemDoneUpdate itemDone:
                var link = FindConsentLink(itemDone.Item);
                if (link is not null)
                {
                    consentLinks.Add(link);
                }
                break;
            case StreamingResponseErrorUpdate errorUpdate:
                Console.WriteLine($"[error] {ModelReaderWriter.Write(errorUpdate, ModelReaderWriterOptions.Json)}");
                break;
            case StreamingResponseCompletedUpdate completed:
                responseId = completed.Response.Id;
                text = completed.Response.GetOutputText() ?? string.Empty;
                break;
        }
    }

    return (responseId, consentLinks.Distinct().ToList(), text);
}

// Serializes an output item to JSON and returns any "consent_link" value found.
static string? FindConsentLink(ResponseItem item)
{
    var json = ModelReaderWriter.Write(item, ModelReaderWriterOptions.Json).ToString();
    using var document = JsonDocument.Parse(json);
    return FindProperty(document.RootElement, "consent_link");
}

// Recursively searches a JSON element for the first string value of a property.
static string? FindProperty(JsonElement element, string name)
{
    switch (element.ValueKind)
    {
        case JsonValueKind.Object:
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals(name) && property.Value.ValueKind == JsonValueKind.String)
                {
                    return property.Value.GetString();
                }

                var nested = FindProperty(property.Value, name);
                if (nested is not null)
                {
                    return nested;
                }
            }
            break;
        case JsonValueKind.Array:
            foreach (var child in element.EnumerateArray())
            {
                var nested = FindProperty(child, name);
                if (nested is not null)
                {
                    return nested;
                }
            }
            break;
    }

    return null;
}
