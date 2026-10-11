using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Crest.Access;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crest.Deployment.Remote.Services;

/// <summary>
/// The remote deployment key as an <c>Api</c>-scheme credential (docs/access.md § Machine
/// callers): a remote instance sends <c>Authorization: RemoteDeployment {clientName}:{key}</c>
/// with its package, the gate authenticates it through the Api forwarder like any bearer
/// call, and the import runs as the remote client's caller. The key never travels in the
/// form any more.
/// </summary>
public sealed class RemoteDeploymentKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    RemoteClientService remoteClients,
    IDataProtectionProvider dataProtectionProvider)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string Scheme = "RemoteDeploymentKey";
    public const string AuthorizationScheme = "RemoteDeployment";

    /// <summary>The name identifier a remote client's principal carries: never a user id.</summary>
    public static string NameIdentifierFor(string clientName) => "remote-client:" + clientName;

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith(AuthorizationScheme + " ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var credential = header[(AuthorizationScheme.Length + 1)..].Trim();
        var separator = credential.IndexOf(':');
        if (separator <= 0)
        {
            return AuthenticateResult.Fail("The remote deployment credential is malformed.");
        }

        var clientName = credential[..separator];
        var apiKey = credential[(separator + 1)..];
        var remoteClient = (await remoteClients.GetRemoteClientListAsync()).RemoteClients.FirstOrDefault(x => x.ClientName == clientName);
        if (remoteClient is null)
        {
            return AuthenticateResult.Fail("The remote client is not known.");
        }

        var protector = dataProtectionProvider.CreateProtector("Crest.Deployment").ToTimeLimitedDataProtector();
        var expected = Encoding.UTF8.GetString(protector.Unprotect(remoteClient.ProtectedApiKey));
        if (!CryptographicEquals(expected, apiKey))
        {
            return AuthenticateResult.Fail("The remote deployment key was not recognized.");
        }

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, NameIdentifierFor(clientName)),
            new Claim(ClaimTypes.Name, clientName),
        ], Scheme);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme));
    }

    private static bool CryptographicEquals(string a, string b)
        => System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}

/// <summary>
/// The remote client's caller: an application caller holding exactly the import permission,
/// no roles. Contributed when the request authenticated through the remote deployment key.
/// </summary>
public sealed class RemoteDeploymentCallerContributor : ICallerContextContributor
{
    public Task ContributeAsync(CallerContextBuilder builder, CancellationToken cancellationToken = default)
    {
        if (builder.Request.Principal?.Identity?.AuthenticationType == RemoteDeploymentKeyAuthenticationHandler.Scheme)
        {
            builder.UserClass = CallerClasses.Application;
            builder.Permissions.Add(DeploymentPermissions.ImportRemoteInstances.Name);
        }

        return Task.CompletedTask;
    }
}
