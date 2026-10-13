using Crest.Access;
using Crest.Admin;
using Crest.ContentManagement;
using Crest.ContentManagement.Display;
using Crest.DisplayManagement.Components;
using Crest.DisplayManagement.ModelBinding;
using Crest.Templates;
using Crest.Templates.Models;
using Crest.Templates.Services;
using Crest.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TemplatesPermissions = Crest.Templates.Permissions;

namespace Crest.Controllers;

/// <summary>
/// The Templates page's API (docs/blazor-display.md § 5, rework step 4): templates are content,
/// so a save makes a draft and publish is its own act; every save is validated against the
/// registry first (security audit item 7: the palette is enforced on the server). A template
/// for the admin theme also takes <c>ManageAdminTemplates</c>. Preview evaluates a draft with
/// the viewer's rights and returns the filtered tree, never cached.
/// </summary>
[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/templates")]
public sealed class TemplatesController(
    TemplateStore store,
    TemplateValidator validator,
    IAuthorizationService authorization,
    IAdminThemeService adminThemes) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<Template[]>> ListAsync()
    {
        if (!await authorization.AuthorizeAsync(User, TemplatesPermissions.ManageTemplates))
        {
            return Forbid();
        }

        var published = (await store.ListPublishedAsync()).ToDictionary(record => record.ContentItemId, StringComparer.Ordinal);
        return Ok((await store.ListLatestAsync()).Select(record => ToModel(record, published.ContainsKey(record.ContentItemId))).ToArray());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Template>> GetAsync(string id)
    {
        if (!await authorization.AuthorizeAsync(User, TemplatesPermissions.ManageTemplates))
        {
            return Forbid();
        }

        var latest = await store.GetAsync(id);
        if (latest is null)
        {
            return NotFound();
        }

        var published = await store.GetAsync(id, VersionOptions.Published);
        return Ok(ToModel(latest, published is not null));
    }

    [HttpPost]
    public Task<ActionResult<Template>> CreateAsync([FromBody] TemplateWrite write, CancellationToken cancellationToken) =>
        SaveAsync(null, write, cancellationToken);

    [HttpPut("{id}")]
    public Task<ActionResult<Template>> UpdateAsync(string id, [FromBody] TemplateWrite write, CancellationToken cancellationToken) =>
        SaveAsync(id, write, cancellationToken);

    [HttpPost("{id}/publish")]
    public async Task<IActionResult> PublishAsync(string id, CancellationToken cancellationToken)
    {
        if (await store.GetAsync(id) is not { } template)
        {
            return NotFound();
        }

        if (!await MayEditAsync(template.Theme))
        {
            return Forbid();
        }

        // A draft that no longer validates (a component removed since) is not published.
        var diagnostics = await validator.ValidateAsync(ToPart(template), cancellationToken);
        if (diagnostics.Count > 0)
        {
            return BadRequest(new TemplateValidation(false, diagnostics));
        }

        await store.PublishAsync(id);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsync(string id)
    {
        if (await store.GetAsync(id) is not { } template)
        {
            return NotFound();
        }

        if (!await MayEditAsync(template.Theme))
        {
            return Forbid();
        }

        await store.RemoveAsync(id);
        return NoContent();
    }

    [HttpPost("validate")]
    public async Task<ActionResult<TemplateValidation>> ValidateAsync([FromBody] TemplateWrite write, CancellationToken cancellationToken)
    {
        if (!await authorization.AuthorizeAsync(User, TemplatesPermissions.ManageTemplates))
        {
            return Forbid();
        }

        var diagnostics = await validator.ValidateAsync(write.ToPart(), cancellationToken);
        return Ok(new TemplateValidation(diagnostics.Count == 0, diagnostics));
    }

    /// <summary>
    /// The draft evaluated as the viewer: bound to its name while the content item's display is
    /// built (the item must be viewable), or on its own. The tree is filtered like any other.
    /// </summary>
    [HttpPost("preview")]
    public async Task<ActionResult<DisplayTree>> PreviewAsync(
        [FromBody] CrestTemplatePreview request,
        [FromServices] TemplatePreview preview,
        [FromServices] TemplateTreeEvaluator evaluator,
        [FromServices] IContentManager contentManager,
        [FromServices] IContentItemDisplayManager displayManager,
        [FromServices] IDisplayTreeBuilder trees,
        [FromServices] IDisplayTreeFilter filter,
        [FromServices] IUpdateModelAccessor updaters,
        [FromServices] ICallerContextAccessor callers,
        [FromServices] IAccessDecision decision,
        CancellationToken cancellationToken)
    {
        if (!await authorization.AuthorizeAsync(User, TemplatesPermissions.ManageTemplates))
        {
            return Forbid();
        }

        var caller = callers.Current;
        DisplayNode root;
        string? displayType = null;
        if (!string.IsNullOrEmpty(request.ContentItemId))
        {
            var item = await contentManager.GetAsync(request.ContentItemId, VersionOptions.Latest);
            if (item is null || caller is null || !await decision.AllowsAsync(caller, Crest.Contents.CommonPermissions.ViewContent.Name, item, cancellationToken))
            {
                return NotFound();
            }

            preview.Name = request.Name;
            preview.Source = request.Source;
            displayType = string.IsNullOrWhiteSpace(request.DisplayType) ? PlatformConstants.DisplayType.Detail : request.DisplayType;
            var shape = await displayManager.BuildDisplayAsync(item, updaters.ModelUpdater, displayType);
            root = (await trees.BuildAsync(shape, cancellationToken: cancellationToken)).Root;
        }
        else
        {
            root = (await evaluator.EvaluateAsync(request.Source, model: null, instanceId: "preview", request.Name, cancellationToken: cancellationToken)).Root;
        }

        var filtered = await filter.FilterAsync(root, caller, cancellationToken) ?? DisplayNode.Markup("preview", string.Empty);
        return Ok(new DisplayTree { Root = filtered, DisplayType = displayType, ScopeSignature = caller?.ScopeSignature });
    }

    private async Task<ActionResult<Template>> SaveAsync(string? id, TemplateWrite write, CancellationToken cancellationToken)
    {
        if (!await MayEditAsync(write.Theme))
        {
            return Forbid();
        }

        if (id is not null && await store.GetAsync(id) is { } existing && !await MayEditAsync(existing.Theme))
        {
            return Forbid();
        }

        var part = write.ToPart();
        var diagnostics = await validator.ValidateAsync(part, cancellationToken);
        if (diagnostics.Count > 0)
        {
            return BadRequest(new TemplateValidation(false, diagnostics));
        }

        var saved = await store.SaveDraftAsync(id, part);

        var published = await store.GetAsync(saved.ContentItemId, VersionOptions.Published);
        return Ok(ToModel(saved, published is not null));
    }

    private async Task<bool> MayEditAsync(string? theme)
    {
        if (!await authorization.AuthorizeAsync(User, TemplatesPermissions.ManageTemplates))
        {
            return false;
        }

        return string.IsNullOrEmpty(theme)
            || !string.Equals(theme, await adminThemes.GetAdminThemeNameAsync(), StringComparison.OrdinalIgnoreCase)
            || await authorization.AuthorizeAsync(User, AdminTemplatesPermissions.ManageAdminTemplates);
    }

    private static Template ToModel(TemplateRecord record, bool published) =>
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
