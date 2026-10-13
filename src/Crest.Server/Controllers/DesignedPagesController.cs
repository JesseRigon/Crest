using System.Text.RegularExpressions;
using Crest.ContentManagement;
using Crest.Display;
using Crest.DisplayManagement.Components;
using Crest.Templates.Models;
using Crest.Templates.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crest.Controllers;

/// <summary>
/// Designed pages (docs/blazor-display.md decision 9, § 6a) and their permission sets (§ 10). A
/// page record is edited directly: a save makes a draft (Design on the page, or
/// <c>ManageDesignedPages</c> to create one), publish is its own act (Publish on the page) and
/// registers its route, preview evaluates a draft as the viewer under the page's Data permission
/// and is never cached. Every save is validated: the name, the route and its bucket, and the
/// tree against the registry like any template.
/// </summary>
[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/pages")]
public sealed partial class DesignedPagesController(
    DesignedPageStore store,
    TemplateValidator validator,
    IAuthorizationService authorization) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DesignedPageModel[]>> ListAsync()
    {
        var published = (await store.ListPublishedAsync()).Select(page => page.ContentItemId).ToHashSet(StringComparer.Ordinal);
        var visible = new List<DesignedPageModel>();
        foreach (var page in await store.ListLatestAsync())
        {
            if (await MayAsync(DesignedPagePermissions.Schema(page.Name)))
            {
                visible.Add(ToModel(page, published.Contains(page.ContentItemId)));
            }
        }

        return Ok(visible.ToArray());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<DesignedPageModel>> GetAsync(string id)
    {
        if (await store.GetAsync(id) is not { } page || !await MayAsync(DesignedPagePermissions.Schema(page.Name)))
        {
            return NotFound();
        }

        return Ok(ToModel(page, await store.GetAsync(id, VersionOptions.Published) is not null));
    }

    [HttpPost]
    public async Task<ActionResult<DesignedPageModel>> CreateAsync([FromBody] DesignedPageWrite write, CancellationToken cancellationToken) =>
        await MayAsync(DesignedPagePermissions.ManageDesignedPages) ? await SaveAsync(null, write, cancellationToken) : Forbid();

    [HttpPut("{id}")]
    public async Task<ActionResult<DesignedPageModel>> UpdateAsync(string id, [FromBody] DesignedPageWrite write, CancellationToken cancellationToken)
    {
        if (await store.GetAsync(id) is not { } existing)
        {
            return NotFound();
        }

        // The name keys the page's permissions: renaming it takes the shell-level set.
        if (!await MayAsync(DesignedPagePermissions.Design(existing.Name))
            || (!string.Equals(existing.Name, write.Name?.Trim(), StringComparison.Ordinal) && !await MayAsync(DesignedPagePermissions.ManageDesignedPages)))
        {
            return Forbid();
        }

        return await SaveAsync(id, write, cancellationToken);
    }

    [HttpPost("{id}/publish")]
    public async Task<IActionResult> PublishAsync(string id, CancellationToken cancellationToken)
    {
        if (await store.GetAsync(id) is not { } page)
        {
            return NotFound();
        }

        if (!await MayAsync(DesignedPagePermissions.Publish(page.Name)))
        {
            return Forbid();
        }

        var problems = await ValidateAsync(ToPart(page), id, cancellationToken);
        if (problems.Count > 0)
        {
            return BadRequest(new DesignedPageValidation(false, problems));
        }

        await store.PublishAsync(id);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsync(string id)
    {
        if (await store.GetAsync(id) is null)
        {
            return NotFound();
        }

        if (!await MayAsync(DesignedPagePermissions.ManageDesignedPages))
        {
            return Forbid();
        }

        await store.RemoveAsync(id);
        return NoContent();
    }

    /// <summary>
    /// A draft rendered as the site would render it at <paramref name="path"/> (its route's values
    /// read from it), with the viewer's rights: Design to see the draft, and the page's Data
    /// permission through the page itself. Never cached.
    /// </summary>
    [HttpPost("{id}/preview")]
    public async Task<ActionResult<SitePage>> PreviewAsync(
        string id,
        [FromQuery] string? path,
        [FromServices] TemplatePreview preview,
        [FromServices] SitePageBuilder pages,
        CancellationToken cancellationToken)
    {
        if (await store.GetAsync(id) is not { } page || !await MayAsync(DesignedPagePermissions.Design(page.Name)))
        {
            return NotFound();
        }

        if (page.Bucket != DesignedPagesConstants.SiteBucket)
        {
            return BadRequest(new DesignedPageValidation(false, [new(page.Name, "bucket", "Only site pages render yet.")]));
        }

        IReadOnlyDictionary<string, string> values = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(path) && !DesignedPageRoutes.TryMatch(page.Route, path, out values))
        {
            return BadRequest(new DesignedPageValidation(false, [new(page.Name, "route", $"'{path}' does not match the route '{page.Route}'.")]));
        }

        preview.Name = page.ShapeName;
        preview.Source = page.Source;
        Response.Headers.CacheControl = "no-store";
        return Ok(await pages.BuildDesignedAsync(page, values, cancellationToken));
    }

    private async Task<ActionResult<DesignedPageModel>> SaveAsync(string? id, DesignedPageWrite write, CancellationToken cancellationToken)
    {
        var part = write.ToPart();
        var problems = await ValidateAsync(part, id, cancellationToken);
        if (problems.Count > 0)
        {
            return BadRequest(new DesignedPageValidation(false, problems));
        }

        var saved = await store.SaveDraftAsync(id, part);
        return Ok(ToModel(saved, await store.GetAsync(saved.ContentItemId, VersionOptions.Published) is not null));
    }

    private async Task<List<DisplayDiagnostic>> ValidateAsync(DesignedPagePart part, string? id, CancellationToken cancellationToken)
    {
        var problems = new List<DisplayDiagnostic>();
        if (!NamePattern().IsMatch(part.Name ?? string.Empty))
        {
            problems.Add(new(part.Name ?? string.Empty, "name", "A page name is letters, digits, '-' and '_' (it keys the page's permissions)."));
        }

        if (!DesignedPagesConstants.IsBucket(part.Bucket))
        {
            problems.Add(new(part.Name ?? string.Empty, "bucket", "The bucket is site, admin or member."));
        }

        if (!DesignedPageRoutes.IsValid(part.Route, out var routeProblem))
        {
            problems.Add(new(part.Name ?? string.Empty, "route", routeProblem!));
        }

        foreach (var other in await store.ListLatestAsync())
        {
            if (other.ContentItemId == id)
            {
                continue;
            }

            if (string.Equals(other.Name, part.Name, StringComparison.OrdinalIgnoreCase))
            {
                problems.Add(new(part.Name!, "name", $"Another page is named '{other.Name}'."));
            }

            if (other.Bucket == part.Bucket && string.Equals(DesignedPageRoutes.Normalize(other.Route), DesignedPageRoutes.Normalize(part.Route), StringComparison.OrdinalIgnoreCase))
            {
                problems.Add(new(part.Name ?? string.Empty, "route", $"The page '{other.Name}' already has the route '{other.Route}'."));
            }
        }

        if (problems.Count == 0)
        {
            problems.AddRange(await validator.ValidateAsync(DesignedPagesConstants.ShapeType + "__" + part.Name, part.Source ?? string.Empty, cancellationToken));
        }

        return problems;
    }

    private async Task<bool> MayAsync(Crest.Security.Permissions.Permission permission) => await authorization.AuthorizeAsync(User, permission);

    private static DesignedPageModel ToModel(DesignedPageRecord page, bool published) =>
        new(page.ContentItemId, page.Name, page.Route, page.Bucket, page.Title, page.Description, page.AllowAnonymous, page.Source, published, !page.Published, page.ModifiedUtc);

    private static DesignedPagePart ToPart(DesignedPageRecord page) => new()
    {
        Name = page.Name,
        Route = page.Route,
        Bucket = page.Bucket,
        Title = page.Title,
        Description = page.Description,
        AllowAnonymous = page.AllowAnonymous,
        Source = page.Source,
    };

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex NamePattern();
}

public sealed record DesignedPageModel(
    string Id,
    string Name,
    string Route,
    string Bucket,
    string? Title,
    string? Description,
    bool AllowAnonymous,
    string Source,
    bool Published,
    bool HasDraft,
    DateTime? ModifiedUtc);

public sealed record DesignedPageWrite(string? Name, string? Route, string? Bucket, string? Title, string? Description, bool AllowAnonymous, string? Source)
{
    public DesignedPagePart ToPart() => new()
    {
        Name = Name?.Trim() ?? string.Empty,
        Route = Route ?? string.Empty,
        Bucket = (Bucket ?? DesignedPagesConstants.SiteBucket).Trim().ToLowerInvariant(),
        Title = Title,
        Description = Description,
        AllowAnonymous = AllowAnonymous,
        Source = Source ?? string.Empty,
    };
}

public sealed record DesignedPageValidation(bool Valid, IReadOnlyList<DisplayDiagnostic> Diagnostics);
