#nullable enable

using Grove.Api;
using Grove.Entities;
using Grove.Communications.Models;
using Grove.Access.Users;
using Grove.Access.Users.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Grove.Communications.Api;

/// <summary>
/// A user's notification method preferences (<see cref="UserNotificationPreferencesFieldSet"/>): the
/// methods the user receives notifications by, in priority order. The signed-in user's own
/// (<c>notifications/preferences/me</c>, the profile settings page) and any user's for a caller who
/// may edit that user (<c>notifications/preferences/users/{id}</c>, the user editor). A user is
/// opted into every method until opting out.
/// </summary>
public sealed class NotificationPreferencesApi : ApiContributor
{
    public override string Name => "NotificationPreferences";

    public override string FeatureId => "Grove.Communications";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("notifications/preferences");
        routes.MapGet("me", ApiHandlers.Handle<NotificationPreferencesHandlers>(handlers => handlers.GetMineAsync()));
        routes.MapPut("me", (HttpContext __http, [FromBody] NotificationPreferencesWrite request) => ApiHandlers.For<NotificationPreferencesHandlers>(__http).SaveMineAsync(request));
        routes.MapGet("users/{id}", (HttpContext __http, [FromRoute] string id) => ApiHandlers.For<NotificationPreferencesHandlers>(__http).GetAsync(id));
        routes.MapPut("users/{id}", (HttpContext __http, [FromRoute] string id, [FromBody] NotificationPreferencesWrite request) => ApiHandlers.For<NotificationPreferencesHandlers>(__http).SaveAsync(id, request));
    }
}

public sealed class NotificationPreferencesHandlers(
    UserManager<IUser> userManager,
    IAuthorizationService authorization,
    IEnumerable<INotificationMethodProvider> providers) : ApiHandlers
{
    public async Task<IResult> GetMineAsync()
    {
        if (User.Identity?.IsAuthenticated != true) return Forbid();
        if (await userManager.GetUserAsync(User) is not User user) return NotFound();
        return Ok(ToModel(user));
    }

    public async Task<IResult> SaveMineAsync([FromBody] NotificationPreferencesWrite request)
    {
        if (User.Identity?.IsAuthenticated != true) return Forbid();
        if (await userManager.GetUserAsync(User) is not User user) return NotFound();
        return await SaveAsync(user, request);
    }

    public async Task<IResult> GetAsync(string id)
    {
        if (await userManager.FindByIdAsync(id) is not User user) return NotFound();
        if (!await authorization.AuthorizeAsync(User, UsersPermissions.EditUsers, user)) return Forbid();
        return Ok(ToModel(user));
    }

    public async Task<IResult> SaveAsync(string id, [FromBody] NotificationPreferencesWrite request)
    {
        if (await userManager.FindByIdAsync(id) is not User user) return NotFound();
        if (!await authorization.AuthorizeAsync(User, UsersPermissions.EditUsers, user)) return Forbid();
        return await SaveAsync(user, request);
    }

    private async Task<IResult> SaveAsync(User user, NotificationPreferencesWrite request)
    {
        var part = user.GetOrCreate<UserNotificationPreferencesFieldSet>();
        var sorted = new List<string>(request.Methods ?? []);
        var selected = new HashSet<string>(request.Enabled ?? [], StringComparer.Ordinal);

        // Every method in the priority order given (the others after, by name), and an opt-out for each one not selected.
        part.Methods = [.. providers
            .OrderBy(provider => sorted.IndexOf(provider.Method) is var index and >= 0 ? index : int.MaxValue)
            .ThenBy(provider => provider.Name.ToString())
            .Select(provider => provider.Method)];
        part.Optout = [.. providers.Where(provider => !selected.Contains(provider.Method)).Select(provider => provider.Method)];
        user.Put(part);

        var result = await userManager.UpdateAsync(user);
        return result.Succeeded ? Ok(ToModel(user)) : BadRequest(result.Errors.Select(error => error.Description).ToArray());
    }

    private NotificationPreferencesModel ToModel(User user)
    {
        user.TryGet<UserNotificationPreferencesFieldSet>(out var part);
        var sorted = new List<string>(part?.Methods ?? []);
        var optout = part?.Optout ?? [];

        // Sorted as the user saved them (the priority), then by name.
        var methods = providers
            .OrderBy(provider => sorted.IndexOf(provider.Method) is var index and >= 0 ? index : int.MaxValue)
            .ThenBy(provider => provider.Name.ToString())
            .Select(provider => new NotificationMethodPreference(provider.Method, provider.Name.ToString(), !optout.Contains(provider.Method)))
            .ToArray();

        return new NotificationPreferencesModel(methods);
    }
}

/// <summary>The notification methods in priority order, each with whether the user receives it.</summary>
public sealed record NotificationPreferencesModel(NotificationMethodPreference[] Methods);

public sealed record NotificationMethodPreference(string Method, string Name, bool Enabled);

/// <param name="Methods">The methods in priority order.</param>
/// <param name="Enabled">The methods the user receives notifications by.</param>
public sealed record NotificationPreferencesWrite(string[]? Methods, string[]? Enabled);
