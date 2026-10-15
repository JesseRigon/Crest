#nullable enable

using System.Text.Json.Nodes;
using System.Text.Json.Settings;
using Grove.Api;
using Grove.ContentManagement;
using Grove.ContentManagement.Metadata.Models;
using Grove.Display;
using Grove.Environment.Shell;
using Grove.Users.Models;
using Grove.Users.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Grove.Users.Api;

/// <summary>
/// A user's custom settings (the Grove.Users.CustomUserSettings feature) on the user editor: the
/// settings types the caller may manage, each type's editor tree (the content editor of the item
/// kept in the user's properties), and saving what the tree's <c>ContentEditor</c> publishes.
/// The permissions the retired settings driver applied: editing the user, and the type's
/// "manage custom user settings" permission. 404 while the feature is disabled.
/// </summary>
internal static class CustomUserSettingsApi
{
    public const string FeatureId = "Grove.Users.CustomUserSettings";

    public static void Map(RouteGroupBuilder users)
    {
        users.MapGet("{id}/custom-settings", (HttpContext __http, [FromRoute] string id) => ApiHandlers.For<CustomUserSettingsHandlers>(__http).ListAsync(id));
        users.MapGet("{id}/custom-settings/{type}/editor", (HttpContext __http, [FromRoute] string id, [FromRoute] string type) => ApiHandlers.For<CustomUserSettingsHandlers>(__http).EditorAsync(id, type));
        users.MapPut("{id}/custom-settings/{type}", (HttpContext __http, [FromRoute] string id, [FromRoute] string type, [FromBody] CustomUserSettingsWrite request) => ApiHandlers.For<CustomUserSettingsHandlers>(__http).SaveAsync(id, type, request));
    }
}

public sealed class CustomUserSettingsHandlers(
    UserManager<IUser> userManager,
    IAuthorizationService authorization,
    IShellFeaturesManager features,
    IServiceProvider services,
    IContentEditorTreeBuilder editors) : ApiHandlers
{
    public async Task<IResult> ListAsync(string id)
    {
        if (await AdmitAsync(id) is not { } admitted) return Denied;

        var list = new List<CustomUserSettingsType>();
        foreach (var type in await admitted.Service.GetAllSettingsTypesAsync())
        {
            if (await authorization.AuthorizeAsync(User, CustomUserSettingsPermissions.CreatePermissionForType(type)))
            {
                list.Add(new CustomUserSettingsType(type.Name, type.DisplayName));
            }
        }

        return Ok(list);
    }

    public async Task<IResult> EditorAsync(string id, string type)
    {
        if (await AdmitAsync(id) is not { } admitted) return Denied;
        if (await AdmitTypeAsync(admitted.Service, type) is not { } definition) return Denied;

        var item = await admitted.Service.GetSettingsAsync(admitted.User, definition);
        var tree = await editors.BuildAsync(item, HttpContext.RequestAborted);
        return tree is null ? NotFound() : Ok(tree);
    }

    public async Task<IResult> SaveAsync(string id, string type, [FromBody] CustomUserSettingsWrite request)
    {
        if (await AdmitAsync(id) is not { } admitted) return Denied;
        if (await AdmitTypeAsync(admitted.Service, type) is not { } definition) return Denied;

        var item = await admitted.Service.GetSettingsAsync(admitted.User, definition);
        item.Content.Clear();
        item.Merge(request.Content?.DeepClone() ?? new JsonObject(), new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Replace });
        admitted.User.Properties[definition.Name] = JObject.FromObject(item);

        var result = await userManager.UpdateAsync(admitted.User);
        return result.Succeeded ? NoContent() : BadRequest(result.Errors.Select(error => error.Description).ToArray());
    }

    // The result of the last refused admission (404 or 403).
    private IResult Denied { get; set; } = TypedResults.NotFound();

    private async Task<(User User, CustomUserSettingsService Service)?> AdmitAsync(string id)
    {
        if (!(await features.GetEnabledFeaturesAsync()).Any(feature => feature.Id == CustomUserSettingsApi.FeatureId)
            || services.GetService(typeof(CustomUserSettingsService)) is not CustomUserSettingsService service
            || await userManager.FindByIdAsync(id) is not User user)
        {
            Denied = NotFound();
            return null;
        }

        if (!await authorization.AuthorizeAsync(User, UsersPermissions.EditUsers, user))
        {
            Denied = Forbid();
            return null;
        }

        return (user, service);
    }

    private async Task<ContentTypeDefinition?> AdmitTypeAsync(CustomUserSettingsService service, string type)
    {
        if (await service.GetSettingsTypeAsync(type) is not { } definition)
        {
            Denied = NotFound();
            return null;
        }

        if (!await authorization.AuthorizeAsync(User, CustomUserSettingsPermissions.CreatePermissionForType(definition)))
        {
            Denied = Forbid();
            return null;
        }

        return definition;
    }
}

public sealed record CustomUserSettingsType(string Name, string DisplayName);

public sealed record CustomUserSettingsWrite(JsonObject? Content);
