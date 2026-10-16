using Grove.Access;
using Grove.Data.Connectors.Services;
using Grove.Environment.Shell;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Grove.Data.Connectors.Tests;

// Credential scopes (staff only; user credentials are the caller's own; the tenant fallback only
// when the policy allows it) and the sealed OAuth state.
public class ScopeAndStateTests
{
    private static readonly CallerContext Staff = new() { Tenant = "t", Side = CallerSide.Admin, UserId = "u1", Permissions = new HashSet<string> { ConnectorsPermissionNames.ConnectOwnAccounts } };
    private static readonly CallerContext Manager = new() { Tenant = "t", Side = CallerSide.Admin, UserId = "admin", Permissions = new HashSet<string> { ConnectorsPermissionNames.ManageConnections } };
    private static readonly CallerContext Member = new() { Tenant = "t", Side = CallerSide.Member, UserId = "m1", UserClass = CallerClasses.Member };
    private static readonly CallerContext Application = new() { Tenant = "t", Side = CallerSide.Site, UserId = "connection:hook", UserClass = CallerClasses.Application };
    private static readonly CallerContext SystemActor = new() { Tenant = "t", Side = CallerSide.System, IsSystem = true };

    [Fact]
    public void Only_staff_users_hold_user_credentials()
    {
        var handler = new UserScopeHandler();
        Assert.Equal(CredentialOwner.ForUser("u1"), handler.OwnerFor(Staff));
        Assert.Null(handler.OwnerFor(Member));
        Assert.Null(handler.OwnerFor(Application));
        Assert.Null(handler.OwnerFor(SystemActor));
    }

    [Fact]
    public void A_user_manages_their_own_credential_and_a_manager_may_revoke_anyones()
    {
        var handler = new UserScopeHandler();
        Assert.True(handler.CanManage(Staff, CredentialOwner.ForUser("u1")));
        Assert.False(handler.CanManage(Staff, CredentialOwner.ForUser("u2")));
        Assert.True(handler.CanManage(Manager, CredentialOwner.ForUser("u2")));
        Assert.False(handler.CanManage(Member, CredentialOwner.ForUser("m1")));
    }

    [Fact]
    public void Tenant_credentials_take_manage_connections()
    {
        var handler = new TenantScopeHandler();
        Assert.True(handler.CanManage(Manager, CredentialOwner.Tenant));
        Assert.False(handler.CanManage(Staff, CredentialOwner.Tenant));
        Assert.Equal(CredentialOwner.Tenant, handler.OwnerFor(Application));
        Assert.Null(handler.OwnerFor(Member));
    }

    [Fact]
    public async Task A_member_never_resolves_a_credential()
    {
        var (resolver, _) = Resolver();
        var result = await resolver.ResolveAsync(Connection(CredentialScope.User), Member);
        Assert.Equal(CredentialResolutionFailure.NotStaff, result.Failure);
    }

    [Fact]
    public async Task A_staff_user_runs_with_their_own_credential()
    {
        var (resolver, store) = Resolver();
        store.RevealAsync("crm", CredentialOwner.ForUser("u1"), Arg.Any<CancellationToken>()).Returns(new ConnectionCredential { Values = new Dictionary<string, string> { ["token"] = "mine" } });
        var result = await resolver.ResolveAsync(Connection(CredentialScope.User), Staff);

        Assert.True(result.Succeeded);
        Assert.Equal(CredentialOwner.ForUser("u1"), result.Owner);
        Assert.Equal("mine", result.Credential!["token"]);
    }

    [Fact]
    public async Task A_user_without_a_credential_never_falls_back_unless_the_policy_allows_it()
    {
        var (resolver, store) = Resolver();
        store.RevealAsync("crm", CredentialOwner.Tenant, Arg.Any<CancellationToken>()).Returns(new ConnectionCredential { Values = new Dictionary<string, string> { ["token"] = "shared" } });

        var strict = await resolver.ResolveAsync(Connection(CredentialScope.User, CredentialScope.Tenant), Staff);
        Assert.Equal(CredentialResolutionFailure.NotConnected, strict.Failure);

        var lenient = await resolver.ResolveAsync(Connection(CredentialScope.User, CredentialScope.Tenant) is var c ? new ConnectionDefinition
        {
            Key = c.Key, DisplayName = c.DisplayName, Protocol = c.Protocol, Auth = c.Auth, AllowedScopes = c.AllowedScopes,
            Policy = new ConnectionPolicy { AllowTenantFallback = true },
        } : null!, Staff);
        Assert.True(lenient.Succeeded);
        Assert.True(lenient.FellBack);
        Assert.Equal(CredentialOwner.Tenant, lenient.Owner);
    }

    [Fact]
    public async Task A_scope_the_connection_does_not_allow_is_refused()
    {
        var (resolver, _) = Resolver();
        var result = await resolver.ResolveAsync(Connection(CredentialScope.Tenant), Staff, CredentialScope.User);
        Assert.Equal(CredentialResolutionFailure.ScopeNotAllowed, result.Failure);
    }

    [Fact]
    public async Task An_unreadable_credential_is_an_explicit_failure()
    {
        var (resolver, store) = Resolver();
        store.RevealAsync("crm", CredentialOwner.Tenant, Arg.Any<CancellationToken>()).Returns<ConnectionCredential?>(_ => throw new CredentialUnreadableException("crm", CredentialOwner.Tenant));
        var result = await resolver.ResolveAsync(Connection(CredentialScope.Tenant), Staff);
        Assert.Equal(CredentialResolutionFailure.Unreadable, result.Failure);
    }

    [Fact]
    public void Scopes_are_open_values()
    {
        var organization = new CredentialScope("Organization");
        Assert.Equal("organization", organization.Value);
        Assert.NotEqual(CredentialScope.Tenant, organization);
        Assert.Equal(CredentialScope.User, new CredentialScope("user"));
    }

    [Fact]
    public void The_oauth_state_round_trips_and_is_bound_to_the_tenant()
    {
        var provider = new EphemeralDataProtectionProvider();
        var options = Options.Create(new ConnectorOptions());
        var protector = new OAuthStateProtector(provider, options, new ShellSettings { Name = "t" });
        var state = new OAuthState("t", "crm", CredentialOwner.ForUser("u1"), "u1", DateTime.UtcNow, "verifier", "/Admin/connected-accounts");

        var sealedState = protector.Protect(state);
        Assert.Equal(state, protector.Unprotect(sealedState));
        Assert.DoesNotContain("verifier", sealedState);

        Assert.Null(protector.Unprotect(sealedState[..^4] + "AAAA"));
        Assert.Null(protector.Unprotect("not-a-state"));
        Assert.Null(new OAuthStateProtector(provider, options, new ShellSettings { Name = "other" }).Unprotect(sealedState));
    }

    private static ConnectionDefinition Connection(params CredentialScope[] scopes) => new()
    {
        Key = "crm",
        DisplayName = "CRM",
        Protocol = ConnectorProtocols.Rest,
        Auth = new ConnectionAuth { Scheme = AuthSchemes.Bearer },
        AllowedScopes = scopes,
    };

    private static (CredentialResolver Resolver, ICredentialStore Store) Resolver()
    {
        var store = Substitute.For<ICredentialStore>();
        return (new CredentialResolver(store, [new TenantScopeHandler(), new UserScopeHandler()]), store);
    }
}

// A connection's machine identity (docs/access.md, Anonymous actors): a webhook's run acts as
// the connection's application caller, named by the connection's key, never as a user.
public class ConnectionCallerTests
{
    [Fact]
    public void The_connection_identity_round_trips_and_is_never_a_user_id()
    {
        var principal = ConnectionCaller.Principal(new ConnectionDefinition { Key = "stripe", DisplayName = "Stripe", Protocol = ConnectorProtocols.Webhook });

        Assert.Equal("stripe", ConnectionCaller.ConnectionKeyOf(principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value));
        Assert.False(principal.Identity!.AuthenticationType!.StartsWith("Identity.", StringComparison.Ordinal));
        Assert.Null(ConnectionCaller.ConnectionKeyOf("u-1"));
        Assert.Null(ConnectionCaller.ConnectionKeyOf("connection:"));
        Assert.Null(ConnectionCaller.ConnectionKeyOf(null));
    }
}
