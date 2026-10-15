using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Grove.Security.Permissions;

namespace Grove.Api;

/// <summary>
/// The single Grove adapter boundary for Grove-authorized work.
/// A domain service is deliberately resolved only after Grove has authorized
/// the current request's real principal for the requested Grove permission.
/// </summary>
public interface IRequestAccess
{
    Task<AuthorizedRequest?> AuthorizeAsync(
        ClaimsPrincipal user,
        Permission permission,
        object? resource = null);
}

public sealed class RequestAccess(
    IAuthorizationService authorization,
    IServiceProvider services) : IRequestAccess
{
    public async Task<AuthorizedRequest?> AuthorizeAsync(
        ClaimsPrincipal user,
        Permission permission,
        object? resource = null)
    {
        if (!await authorization.AuthorizeAsync(user, permission, resource))
        {
            return null;
        }

        return new AuthorizedRequest(services);
    }
}

/// <summary>
/// A successful Grove authorization. Its service accessor is intentionally
/// unavailable until <see cref="IRequestAccess.AuthorizeAsync"/> succeeds.
/// </summary>
public sealed class AuthorizedRequest
{
    private readonly IServiceProvider _services;

    internal AuthorizedRequest(IServiceProvider services) => _services = services;

    public TService GetRequiredService<TService>() where TService : notnull =>
        _services.GetRequiredService<TService>();

    public TService? GetService<TService>() where TService : class =>
        _services.GetService<TService>();
}
