#nullable enable

using System.Text.Json.Nodes;
using Grove.Api;
using Grove.Infrastructure.Settings.Groups;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Grove.Infrastructure.Settings.Api;

/// <summary>
/// The settings groups' API (docs/MVC/api.md, docs/MVC/parity.md › SettingsForm): the groups the
/// caller may manage, and per group its form (schema, values, which secrets are set) and its save.
/// A caller is admitted by any permission the group registered (<c>ManageGroupSettings</c> asked
/// against the group id, as every settings group is).
/// </summary>
public sealed class SettingsGroupsApi : ApiContributor
{
    public override string Name => "SettingsGroups";

    public override string FeatureId => "Grove.Infrastructure.Settings";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("settings/groups");
        routes.MapGet("", ApiHandlers.Handle<SettingsGroupsHandlers>(h => h.ListAsync()));
        routes.MapGet("{groupId}", (HttpContext __http, [FromRoute] string groupId) => ApiHandlers.For<SettingsGroupsHandlers>(__http).GetAsync(groupId));
        routes.MapPut("{groupId}", (HttpContext __http, [FromRoute] string groupId, [FromBody] SettingsGroupUpdate update) => ApiHandlers.For<SettingsGroupsHandlers>(__http).PutAsync(groupId, update));
    }
}

public sealed record SettingsGroupUpdate(JsonObject? Values);

public sealed record SettingsGroupSummary(string Id, string Caption, string? Description);

public sealed record SettingsGroupSaved(JsonObject Values, IReadOnlyDictionary<string, bool> Secrets, bool Changed, bool Reloaded);

public sealed class SettingsGroupsHandlers(SettingsGroupService groups, IAuthorizationService authorization) : ApiHandlers
{
    public async Task<IResult> ListAsync()
    {
        var allowed = new List<SettingsGroupSummary>();
        foreach (var group in groups.Groups.OrderBy(group => group.Caption, StringComparer.CurrentCultureIgnoreCase))
        {
            if (await CanManageAsync(group))
            {
                allowed.Add(new SettingsGroupSummary(group.Id, group.Caption, group.Description));
            }
        }

        return Ok(allowed);
    }

    public async Task<IResult> GetAsync(string groupId)
    {
        if (groups.Find(groupId) is not { } group)
        {
            return ApiProblems.NotFound($"There is no settings group '{groupId}'.");
        }

        if (!await CanManageAsync(group))
        {
            return Forbid();
        }

        return Ok(await groups.GetFormAsync(group, HttpContext.RequestAborted));
    }

    public async Task<IResult> PutAsync(string groupId, SettingsGroupUpdate update)
    {
        if (groups.Find(groupId) is not { } group)
        {
            return ApiProblems.NotFound($"There is no settings group '{groupId}'.");
        }

        if (!await CanManageAsync(group))
        {
            return Forbid();
        }

        var result = await groups.SaveAsync(group, update.Values ?? [], HttpContext.RequestAborted);
        if (!result.Succeeded)
        {
            return ApiProblems.Validation(result.Errors);
        }

        return Ok(new SettingsGroupSaved(result.Values!, result.Secrets!, result.Changed, result.Reloaded));
    }

    private async Task<bool> CanManageAsync(SettingsGroup group) =>
        await authorization.AuthorizeAsync(User, SettingsPermissions.ManageGroupSettings, (object)group.Id);
}
