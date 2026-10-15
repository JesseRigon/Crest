#nullable enable

using Grove.ContentGroups;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Grove.Api;
using Microsoft.Extensions.DependencyInjection;
using Grove.Contents;
using Grove.ContentTypes;
using Grove.Environment.Shell.Scope;

namespace Grove.ContentTypes.Api;

public sealed class ContentGroupsApi : ApiContributor
{
    public override string Name => "ContentGroups";

    public override string FeatureId => "Grove.ContentTypes";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("content-groups");
        routes.MapGet("", ApiHandlers.Handle<ContentGroupsHandlers>(h => h.ListAsync()));
        routes.MapGet("settings", ApiHandlers.Handle<ContentGroupsHandlers>(h => h.GetSettingsAsync()));
        routes.MapPut("settings", (HttpContext __http, [FromBody] ContentGroupsSettingsRequest request) => ApiHandlers.For<ContentGroupsHandlers>(__http).PutSettingsAsync(request));
        routes.MapPost("", (HttpContext __http, [FromBody] CreateContentGroupRequest request) => ApiHandlers.For<ContentGroupsHandlers>(__http).CreateAsync(request));
        routes.MapPut("{key}", (HttpContext __http, [FromRoute] string key, [FromBody] UpdateContentGroupRequest request) => ApiHandlers.For<ContentGroupsHandlers>(__http).UpdateAsync(key, request));
        routes.MapPost("{key}/entries", (HttpContext __http, [FromRoute] string key, [FromBody] AddContentGroupEntryRequest request) => ApiHandlers.For<ContentGroupsHandlers>(__http).AddEntryAsync(key, request));
        routes.MapDelete("{key}/entries/{kind}/{entryKey}", (HttpContext __http, [FromRoute] string key, [FromRoute] string kind, [FromRoute] string entryKey) => ApiHandlers.For<ContentGroupsHandlers>(__http).RemoveEntryAsync(key, kind, entryKey));
        routes.MapDelete("{key}", (HttpContext __http, [FromRoute] string key) => ApiHandlers.For<ContentGroupsHandlers>(__http).DeleteAsync(key));
    }
}


public sealed record CreateContentGroupRequest(string Key, string DisplayName, int Position = 0);
public sealed record UpdateContentGroupRequest(string? DisplayName, int? Position, bool? Hidden);
public sealed record AddContentGroupEntryRequest(string Kind, string Key);
public sealed record ContentGroupsSettingsRequest(bool AutoMenuPages);

/// <summary>
/// Content groups: reading is gated like the content-items list (ListContent);
/// reshaping groups is a definition-level act (EditContentTypes). Every write that can
/// change the menu (when auto pages are on) re-syncs the materialized menu as a
/// DEFERRED task, for the read-after-write reason documented on the per-type toggle.
/// </summary>
public sealed class ContentGroupsHandlers(
    ContentGroupService groups,
    IAuthorizationService authorization) : ApiHandlers
{
    public async Task<IResult> ListAsync()
    {
        if (!await authorization.AuthorizeAsync(User, CommonPermissions.ListContent)) return Forbid();

        return Ok(await groups.ListAsync(HttpContext.RequestAborted));
    }

    public async Task<IResult> GetSettingsAsync()
    {
        if (!await authorization.AuthorizeAsync(User, CommonPermissions.ListContent)) return Forbid();

        return Ok(await groups.GetSettingsAsync());
    }

    public async Task<IResult> PutSettingsAsync(ContentGroupsSettingsRequest request)
    {
        if (!await authorization.AuthorizeAsync(User, ContentTypesPermissions.EditContentTypes)) return Forbid();

        var settings = await groups.UpdateSettingsAsync(request.AutoMenuPages);
        return Ok(settings);
    }

    public Task<IResult> CreateAsync(CreateContentGroupRequest request)
        => WriteAsync(() => groups.CreateGroupAsync(request.Key, request.DisplayName, request.Position, HttpContext.RequestAborted));

    public Task<IResult> UpdateAsync(string key, UpdateContentGroupRequest request)
        => WriteAsync(() => groups.UpdateGroupAsync(key, request.DisplayName, request.Position, request.Hidden, HttpContext.RequestAborted));

    public Task<IResult> AddEntryAsync(string key, AddContentGroupEntryRequest request)
        => WriteAsync(() => groups.AddEntryAsync(key, request.Kind, request.Key, HttpContext.RequestAborted));

    public async Task<IResult> RemoveEntryAsync(string key, string kind, string entryKey)
    {
        if (!await authorization.AuthorizeAsync(User, ContentTypesPermissions.EditContentTypes)) return Forbid();

        var group = await groups.RemoveEntryAsync(key, kind, entryKey, HttpContext.RequestAborted);
        if (group is null) return NotFound();

        return Ok(group);
    }

    public async Task<IResult> DeleteAsync(string key)
    {
        if (!await authorization.AuthorizeAsync(User, ContentTypesPermissions.EditContentTypes)) return Forbid();

        if (!await groups.DeleteGroupAsync(key, HttpContext.RequestAborted)) return NotFound();

        return NoContent();
    }

    private async Task<IResult> WriteAsync(Func<Task<ContentGroupModel>> write)
    {
        if (!await authorization.AuthorizeAsync(User, ContentTypesPermissions.EditContentTypes)) return Forbid();

        try
        {
            var group = await write();
            return Ok(group);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { errors = new[] { ex.Message } });
        }
    }
}
