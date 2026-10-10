using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Crest.Security.Permissions;

namespace Crest.Services;

/// <summary>
/// The single Crest adapter boundary for Crest-authorized work.
/// A domain service is deliberately resolved only after Crest has authorized
/// the current request's real principal for the requested Crest permission.
/// </summary>
public interface ICrestRequestAccess
{
    Task<CrestAuthorizedRequest?> AuthorizeAsync(
        ClaimsPrincipal user,
        Permission permission,
        object? resource = null);
}

public sealed class CrestRequestAccess(
    IAuthorizationService authorization,
    IServiceProvider services) : ICrestRequestAccess
{
    public async Task<CrestAuthorizedRequest?> AuthorizeAsync(
        ClaimsPrincipal user,
        Permission permission,
        object? resource = null)
    {
        if (!await authorization.AuthorizeAsync(user, permission, resource))
        {
            return null;
        }

        return new CrestAuthorizedRequest(services);
    }
}

/// <summary>
/// A successful Crest authorization. Its service accessor is intentionally
/// unavailable until <see cref="ICrestRequestAccess.AuthorizeAsync"/> succeeds.
/// </summary>
public sealed class CrestAuthorizedRequest
{
    private readonly IServiceProvider _services;

    internal CrestAuthorizedRequest(IServiceProvider services) => _services = services;

    public TService GetRequiredService<TService>() where TService : notnull =>
        _services.GetRequiredService<TService>();

    public TService? GetService<TService>() where TService : class =>
        _services.GetService<TService>();
}
