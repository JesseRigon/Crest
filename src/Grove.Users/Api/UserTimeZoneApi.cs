#nullable enable

using Grove.Api;
using Grove.Entities;
using Grove.Modules;
using Grove.Users.Models;
using Grove.Users.TimeZone.Models;
using Grove.Users.TimeZone.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Grove.Users.Api;

/// <summary>
/// The signed-in user's own time zone (<c>api/grove/time-zone/me</c>, the Grove.Users.TimeZone
/// feature): the profile settings page's time zone select, replacing the user editor's time zone
/// section. Self-service, like the user's default culture.
/// </summary>
public sealed class UserTimeZoneApi : ApiContributor
{
    public override string Name => "UserTimeZone";

    public override string FeatureId => "Grove.Users.TimeZone";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("time-zone/me");
        routes.MapGet("", ApiHandlers.Handle<UserTimeZoneHandlers>(handlers => handlers.GetAsync()));
        routes.MapPut("", (HttpContext __http, [FromBody] UserTimeZoneWrite request) => ApiHandlers.For<UserTimeZoneHandlers>(__http).SetAsync(request));
    }
}

public sealed class UserTimeZoneHandlers(
    UserManager<IUser> userManager,
    IUserTimeZoneService timeZoneService,
    ITimeZoneSelectListProvider timeZones) : ApiHandlers
{
    public async Task<IResult> GetAsync()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Forbid();
        }

        if (await userManager.GetUserAsync(User) is not User user)
        {
            return NotFound();
        }

        user.TryGet<UserTimeZone>(out var timeZone);
        return Ok(await ToModelAsync(timeZone?.TimeZoneId));
    }

    public async Task<IResult> SetAsync([FromBody] UserTimeZoneWrite request)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Forbid();
        }

        if (await userManager.GetUserAsync(User) is not User user)
        {
            return NotFound();
        }

        var timeZoneId = string.IsNullOrWhiteSpace(request.TimeZoneId) ? null : request.TimeZoneId.Trim();
        if (timeZoneId is not null && !(await timeZones.GetTimeZoneSelectListAsync(HttpContext.RequestAborted)).Any(option => option.Key == timeZoneId))
        {
            return BadRequest("The time zone is unknown.");
        }

        user.Alter<UserTimeZone>(timeZone => timeZone.TimeZoneId = timeZoneId);
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        await timeZoneService.UpdateAsync(user);
        return Ok(await ToModelAsync(timeZoneId));
    }

    private async Task<UserTimeZoneModel> ToModelAsync(string? timeZoneId) =>
        new(timeZoneId, [.. (await timeZones.GetTimeZoneSelectListAsync(HttpContext.RequestAborted)).Select(option => new UserTimeZoneOption(option.Key, option.Value))]);
}

/// <summary>The user's time zone (null: the site's) and the time zones to choose from.</summary>
public sealed record UserTimeZoneModel(string? TimeZoneId, UserTimeZoneOption[] TimeZones);

public sealed record UserTimeZoneOption(string Id, string Name);

public sealed record UserTimeZoneWrite(string? TimeZoneId);
