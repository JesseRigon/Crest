#nullable enable

using System.Security.Claims;
using Grove.Access;
using Grove.Infrastructure.Shells.Admin;
using Grove.Api;
using Grove.Contents.Management;
using Grove.Contents.Management.Display;
using Grove.Display.Management.Components;
using Grove.Display.Templates.Models;
using Grove.Display.Templates.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Display.Templates.Api;

/// <summary>
/// The Templates page's API (docs/blazor-display.md § 5, rework step 4): templates are content,
/// so a save makes a draft and publish is its own act; every save is validated against the
/// registry first (security audit item 7: the palette is enforced on the server). A template
/// for the admin theme also takes <c>ManageAdminTemplates</c>. Preview evaluates a draft with
/// the viewer's rights and returns the filtered tree, never cached. The overrides
/// (<c>templates/overrides</c>) are <see cref="ComponentOverridesApi"/>'s.
/// </summary>
public sealed class TemplatesApi : ApiContributor
{
    public override string Name => "Templates";

    public override string FeatureId => "Grove.Display.Templates";

    public override void Map(IEndpointRouteBuilder group)
    {
        var templates = group.MapGroup("templates");
        templates.MapGet("", ListAsync);
        templates.MapGet("{id}", GetAsync);
        templates.MapPost("", ([FromBody] TemplateWrite write, HttpContext context, CancellationToken cancellationToken) => SaveAsync(null, write, context, cancellationToken));
        templates.MapPut("{id}", ([FromRoute] string id, [FromBody] TemplateWrite write, HttpContext context, CancellationToken cancellationToken) => SaveAsync(id, write, context, cancellationToken));
        templates.MapPost("{id}/publish", PublishAsync);
        templates.MapDelete("{id}", DeleteAsync);
        templates.MapPost("validate", ValidateAsync);
        templates.MapPost("preview", PreviewAsync);
    }

    private static async Task<IResult> ListAsync(ClaimsPrincipal user, [FromServices] TemplateStore store, [FromServices] IAuthorizationService authorization)
    {
        if (!await authorization.AuthorizeAsync(user, Permissions.ManageTemplates))
        {
            return TypedResults.Forbid();
        }

        var published = (await store.ListPublishedAsync()).ToDictionary(record => record.ContentItemId, StringComparer.Ordinal);
        return TypedResults.Ok((await store.ListLatestAsync()).Select(record => ToModel(record, published.ContainsKey(record.ContentItemId))).ToArray());
    }

    private static async Task<IResult> GetAsync([FromRoute] string id, ClaimsPrincipal user, [FromServices] TemplateStore store, [FromServices] IAuthorizationService authorization)
    {
        if (!await authorization.AuthorizeAsync(user, Permissions.ManageTemplates))
        {
            return TypedResults.Forbid();
        }

        var latest = await store.GetAsync(id);
        if (latest is null)
        {
            return TypedResults.NotFound();
        }

        var published = await store.GetAsync(id, VersionOptions.Published);
        return TypedResults.Ok(ToModel(latest, published is not null));
    }

    private static async Task<IResult> PublishAsync([FromRoute] string id, HttpContext context, [FromServices] TemplateStore store, [FromServices] TemplateValidator validator, CancellationToken cancellationToken)
    {
        if (await store.GetAsync(id) is not { } template)
        {
            return TypedResults.NotFound();
        }

        if (!await MayEditAsync(context, template.Theme))
        {
            return TypedResults.Forbid();
        }

        // A draft that no longer validates (a component removed since) is not published.
        var diagnostics = await validator.ValidateAsync(ToPart(template), cancellationToken);
        if (diagnostics.Count > 0)
        {
            return TypedResults.BadRequest(new TemplateValidation(false, diagnostics));
        }

        await store.PublishAsync(id);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> DeleteAsync([FromRoute] string id, HttpContext context, [FromServices] TemplateStore store)
    {
        if (await store.GetAsync(id) is not { } template)
        {
            return TypedResults.NotFound();
        }

        if (!await MayEditAsync(context, template.Theme))
        {
            return TypedResults.Forbid();
        }

        await store.RemoveAsync(id);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> ValidateAsync([FromBody] TemplateWrite write, ClaimsPrincipal user, [FromServices] IAuthorizationService authorization, [FromServices] TemplateValidator validator, CancellationToken cancellationToken)
    {
        if (!await authorization.AuthorizeAsync(user, Permissions.ManageTemplates))
        {
            return TypedResults.Forbid();
        }

        var diagnostics = await validator.ValidateAsync(write.ToPart(), cancellationToken);
        return TypedResults.Ok(new TemplateValidation(diagnostics.Count == 0, diagnostics));
    }

    /// <summary>
    /// The draft evaluated as the viewer: bound to its name while the content item's display is
    /// built (the item must be viewable), or on its own. The tree is filtered like any other.
    /// </summary>
    private static async Task<IResult> PreviewAsync(
        [FromBody] TemplatePreviewRequest request,
        ClaimsPrincipal user,
        [FromServices] IAuthorizationService authorization,
        [FromServices] TemplatePreview preview,
        [FromServices] TemplateTreeEvaluator evaluator,
        [FromServices] IContentManager contentManager,
        [FromServices] IContentItemDisplayManager displayManager,
        [FromServices] IDisplayTreeBuilder trees,
        [FromServices] IDisplayTreeFilter filter,
        [FromServices] ICallerContextAccessor callers,
        [FromServices] IAccessDecision decision,
        CancellationToken cancellationToken)
    {
        if (!await authorization.AuthorizeAsync(user, Permissions.ManageTemplates))
        {
            return TypedResults.Forbid();
        }

        var caller = callers.Current;
        DisplayNode root;
        string? displayType = null;
        if (!string.IsNullOrEmpty(request.ContentItemId))
        {
            var item = await contentManager.GetAsync(request.ContentItemId, VersionOptions.Latest);
            if (item is null || caller is null || !await decision.AllowsAsync(caller, Grove.Contents.CommonPermissions.ViewContent.Name, item, cancellationToken))
            {
                return TypedResults.NotFound();
            }

            preview.Name = request.Name;
            preview.Source = request.Source;
            displayType = string.IsNullOrWhiteSpace(request.DisplayType) ? PlatformConstants.DisplayType.Detail : request.DisplayType;
            var shape = await displayManager.BuildDisplayAsync(item, displayType);
            root = (await trees.BuildAsync(shape, cancellationToken: cancellationToken)).Root;
        }
        else
        {
            root = (await evaluator.EvaluateAsync(request.Source, model: null, instanceId: "preview", request.Name, cancellationToken: cancellationToken)).Root;
        }

        var filtered = await filter.FilterAsync(root, caller, cancellationToken) ?? DisplayNode.Markup("preview", string.Empty);
        return TypedResults.Ok(new DisplayTree { Root = filtered, DisplayType = displayType, ScopeSignature = caller?.ScopeSignature });
    }

    private static async Task<IResult> SaveAsync(string? id, TemplateWrite write, HttpContext context, CancellationToken cancellationToken)
    {
        var store = context.RequestServices.GetRequiredService<TemplateStore>();
        var validator = context.RequestServices.GetRequiredService<TemplateValidator>();

        if (!await MayEditAsync(context, write.Theme))
        {
            return TypedResults.Forbid();
        }

        if (id is not null && await store.GetAsync(id) is { } existing && !await MayEditAsync(context, existing.Theme))
        {
            return TypedResults.Forbid();
        }

        var part = write.ToPart();
        var diagnostics = await validator.ValidateAsync(part, cancellationToken);
        if (diagnostics.Count > 0)
        {
            return TypedResults.BadRequest(new TemplateValidation(false, diagnostics));
        }

        var saved = await store.SaveDraftAsync(id, part);

        var published = await store.GetAsync(saved.ContentItemId, VersionOptions.Published);
        return TypedResults.Ok(ToModel(saved, published is not null));
    }

    private static async Task<bool> MayEditAsync(HttpContext context, string? theme)
    {
        var authorization = context.RequestServices.GetRequiredService<IAuthorizationService>();
        if (!await authorization.AuthorizeAsync(context.User, Permissions.ManageTemplates))
        {
            return false;
        }

        return string.IsNullOrEmpty(theme)
            || !string.Equals(theme, await context.RequestServices.GetRequiredService<IAdminThemeService>().GetAdminThemeNameAsync(), StringComparison.OrdinalIgnoreCase)
            || await authorization.AuthorizeAsync(context.User, AdminTemplatesPermissions.ManageAdminTemplates);
    }

    private static TemplateModel ToModel(TemplateRecord record, bool published) =>
        new(record.ContentItemId, record.Name, record.Theme, record.DisplayType, record.Description, record.Source, published, !record.Published, record.ModifiedUtc,
            record.IsComposite, record.DisplayName, record.Category, record.Icon, record.Inputs, record.Outputs);

    private static TemplatePart ToPart(TemplateRecord record) => new()
    {
        Name = record.Name,
        Theme = record.Theme,
        DisplayType = record.DisplayType,
        Description = record.Description,
        Source = record.Source,
        IsComposite = record.IsComposite,
        DisplayName = record.DisplayName,
        Category = record.Category,
        Icon = record.Icon,
        Inputs = [.. record.Inputs],
        Outputs = [.. record.Outputs],
    };
}

/// <summary>A template as the Templates page lists it (docs/blazor-display.md § 5).</summary>
public sealed record TemplateModel(
    string Id,
    string Name,
    string? Theme,
    string? DisplayType,
    string? Description,
    string Source,
    bool Published,
    bool HasDraft,
    DateTime? ModifiedUtc,
    bool IsComposite,
    string? DisplayName,
    string? Category,
    string? Icon,
    IReadOnlyList<TemplateInput> Inputs,
    IReadOnlyList<TemplateOutput> Outputs);

public sealed record TemplateWrite(
    string Name,
    string? Theme,
    string? DisplayType,
    string? Description,
    string? Source,
    bool IsComposite = false,
    string? DisplayName = null,
    string? Category = null,
    string? Icon = null,
    List<TemplateInput>? Inputs = null,
    List<TemplateOutput>? Outputs = null)
{
    public TemplatePart ToPart() => new()
    {
        Name = Name,
        Theme = Theme,
        DisplayType = DisplayType,
        Description = Description?.Trim(),
        Source = Source ?? string.Empty,
        IsComposite = IsComposite,
        DisplayName = DisplayName,
        Category = Category,
        Icon = Icon,
        Inputs = Inputs ?? [],
        Outputs = Outputs ?? [],
    };
}

/// <summary>Evaluates a draft for preview: bound to its name for one content item, or on its own.</summary>
public sealed record TemplatePreviewRequest(string Name, string Source, string? ContentItemId, string? DisplayType);

public sealed record TemplateValidation(bool Valid, IReadOnlyList<DisplayDiagnostic> Diagnostics);
