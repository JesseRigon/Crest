using System.Globalization;
using System.Security.Claims;
using Crest.Users;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Crest.Access.Services;

/// <summary>
/// What a connection that outlives a request (a SignalR hub connection, a Blazor Server
/// circuit) keeps of the request that opened it: the side and organization the gate admitted
/// it on, and when its security stamp was last checked. Never rights: those are rebuilt per
/// operation (docs/workflows.md › Caller lifetime).
/// </summary>
public sealed class ConnectionCallerState
{
    public required CallerSide Side { get; init; }

    public string? OrganizationId { get; init; }

    public string? Culture { get; init; }

    public DateTimeOffset StampValidatedAt { get; set; }

    /// <summary>The state of the request that opened the connection: the caller the gate
    /// built for it names the side and organization; a request the gate never saw is Site.</summary>
    public static ConnectionCallerState From(HttpContext? context, TimeProvider time)
    {
        var opener = context?.RequestServices.GetService<ICallerContextAccessor>()?.Current;
        return new ConnectionCallerState
        {
            Side = opener?.Side ?? AccessGate.SideOf(context),
            OrganizationId = opener?.OrganizationId,
            Culture = opener?.Culture ?? CultureInfo.CurrentUICulture.Name,
            StampValidatedAt = time.GetUtcNow(),
        };
    }
}

/// <summary>A connection's identity no longer holds: the stamp changed (sign-out
/// everywhere, a password or credential change), or the user is gone or disabled.</summary>
public sealed class ConnectionRevokedException(string message) : Exception(message);

/// <summary>
/// Builds the caller for one operation on a long-lived connection: the security stamp is
/// re-validated once the stamp interval has passed since the last check, then the caller is
/// built afresh (under the current permission version, so a right lost between two
/// operations fails the next one) and handed to <see cref="ICallerContextAccessor"/>.
/// Throws <see cref="ConnectionRevokedException"/> or <see cref="CallerDeniedException"/>;
/// the connection's host tears the connection down on either.
/// </summary>
public static class ConnectionCallers
{
    public static async Task<CallerContext> EnterAsync(IServiceProvider services, ClaimsPrincipal? principal, ConnectionCallerState state, CancellationToken cancellationToken = default)
    {
        var time = services.GetService<TimeProvider>() ?? TimeProvider.System;
        var interval = services.GetService<IOptions<SecurityStampValidatorOptions>>()?.Value.ValidationInterval ?? TimeSpan.FromMinutes(30);
        var now = time.GetUtcNow();
        if (IsIdentityCookie(principal) && now - state.StampValidatedAt >= interval)
        {
            var signIn = services.GetService<SignInManager<IUser>>();
            if (signIn is not null && await signIn.ValidateSecurityStampAsync(principal) is null)
            {
                throw new ConnectionRevokedException("The session's security stamp no longer matches.");
            }

            state.StampValidatedAt = now;
        }

        var caller = await services.GetRequiredService<ICallerContextFactory>().CreateAsync(
            new CallerRequest(principal, state.Side, state.OrganizationId, state.Culture),
            cancellationToken);
        services.GetRequiredService<ICallerContextAccessor>().Current = caller;
        return caller;
    }

    public static void Leave(IServiceProvider services) =>
        services.GetRequiredService<ICallerContextAccessor>().Current = null;

    /// <summary>Only a user's cookie identity carries a stamp; an application's bearer
    /// token is short-lived and re-validated by its own scheme.</summary>
    private static bool IsIdentityCookie(ClaimsPrincipal? principal) =>
        principal?.Identity?.IsAuthenticated == true
        && principal.Identities.Any(identity => identity.AuthenticationType is { } type && type.StartsWith("Identity.", StringComparison.Ordinal));
}

/// <summary>
/// Every SignalR hub: the caller is built per invocation, not once for the connection; the
/// stamp is re-validated on its interval; a revoked identity or a refused caller aborts the
/// connection.
/// </summary>
public sealed class ConnectionCallerHubFilter : IHubFilter
{
    private const string StateKey = "Crest.Access.ConnectionCaller";

    public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
    {
        var services = context.ServiceProvider;
        var state = ConnectionCallerState.From(context.Context.GetHttpContext(), services.GetService<TimeProvider>() ?? TimeProvider.System);
        context.Context.Items[StateKey] = state;
        if (!await TryEnterAsync(context.Context, services, state))
        {
            return;
        }

        try
        {
            await next(context);
        }
        finally
        {
            ConnectionCallers.Leave(services);
        }
    }

    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var hubContext = invocationContext.Context;
        var services = invocationContext.ServiceProvider;
        if (hubContext.Items.TryGetValue(StateKey, out var value) && value is ConnectionCallerState state)
        {
            if (!await TryEnterAsync(hubContext, services, state))
            {
                throw new HubException("The connection's caller is no longer admitted.");
            }

            try
            {
                return await next(invocationContext);
            }
            finally
            {
                ConnectionCallers.Leave(services);
            }
        }

        // A connection that never passed OnConnectedAsync through this filter: refused.
        hubContext.Abort();
        throw new HubException("The connection was not admitted.");
    }

    private static async Task<bool> TryEnterAsync(HubCallerContext hubContext, IServiceProvider services, ConnectionCallerState state)
    {
        try
        {
            await ConnectionCallers.EnterAsync(services, hubContext.User, state, hubContext.ConnectionAborted);
            return true;
        }
        catch (Exception ex) when (ex is ConnectionRevokedException or CallerDeniedException)
        {
            hubContext.Abort();
            return false;
        }
    }
}

/// <summary>
/// Every Blazor Server circuit: the caller is built per inbound activity (a UI event, a JS
/// interop callback), from the circuit's authentication state and the side the circuit was
/// admitted on. A revoked identity or a refused caller throws out of the activity, which
/// tears the circuit down.
/// </summary>
public sealed class ConnectionCallerCircuitHandler(
    IServiceProvider services,
    IHttpContextAccessor httpContextAccessor,
    AuthenticationStateProvider authentication) : CircuitHandler
{
    // Built when the circuit's scope is created, inside the hub call that starts the circuit,
    // where the circuit hub's own request is still the current one.
    private readonly ConnectionCallerState _state = ConnectionCallerState.From(
        httpContextAccessor.HttpContext,
        services.GetService<TimeProvider>() ?? TimeProvider.System);

    public override Func<CircuitInboundActivityContext, Task> CreateInboundActivityHandler(Func<CircuitInboundActivityContext, Task> next) =>
        async context =>
        {
            var principal = (await authentication.GetAuthenticationStateAsync()).User;
            await ConnectionCallers.EnterAsync(services, principal, _state);
            try
            {
                await next(context);
            }
            finally
            {
                ConnectionCallers.Leave(services);
            }
        };
}
