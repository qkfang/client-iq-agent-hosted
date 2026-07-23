using Azure.Core;

namespace Onboarding.Web.Services;

/// <summary>
/// Wraps an already-acquired access token as a <see cref="TokenCredential"/> so
/// it can be handed to an <c>AIProjectClient</c>. Used to run the Foundry agent
/// with the signed-in user's token instead of the app identity.
/// </summary>
public sealed class StaticTokenCredential : TokenCredential
{
    private readonly AccessToken _token;

    public StaticTokenCredential(string accessToken, DateTimeOffset expiresOn)
        => _token = new AccessToken(accessToken, expiresOn);

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        => _token;

    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        => new(_token);
}
