#nullable enable

using System.Security.Claims;
using System.Text.RegularExpressions;
using Crest.Api;
using Crest.ContentManagement;
using Crest.DisplayManagement.Components;
using Crest.Templates.Models;
using Crest.Templates.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Templates.Api;

/// <summary>
/// Designed pages (docs/blazor-display.md decision 9, § 6a) and their permission sets (§ 10). A
/// page record is edited directly: a save makes a draft (Design on the page, or
/// <c>ManageDesignedPages</c> to create one), publish is its own act (Publish on the page) and
/// registers its route. Every save is validated: the name, the route and its bucket, and the
/// tree against the registry like any template. The draft preview renders the site page, so it is
/// the server's display endpoint (<c>pages/{id}/preview</c>).
/// </summary>
public sealed partial class DesignedPagesApi : ApiContributor
{
    public override string Name => "DesignedPages";

    public override string FeatureId => "Crest.Templates";

    public override void Map(IEndpointRouteBuilder group)
    {
        var pages = group.MapGroup("pages");
        pages.MapGet("", ListAsync);
        pages.MapGet("{id}", GetAsync);
        pages.MapPost("", CreateAsync);
        pages.MapPut("{id}", UpdateAsync);
        pages.MapPost("{id}/publish", PublishAsync);
        pages.MapDelete("{id}", DeleteAsync);
    }

    private static async Task<IResult> ListAsync(HttpContext context, [FromServices] DesignedPageStore store)
    {
        var published = (await store.ListPublishedAsync()).Select(page => page.ContentItemId).ToHashSet(StringComparer.Ordinal);
        var visible = new List<DesignedPageModel>();
        foreach (var page in await store.ListLatestAsync())
        {
            if (await MayAsync(context, DesignedPagePermissions.Schema(page.Name)))
            {
                visible.Add(ToModel(page, published.Contains(page.ContentItemId)));
            }
        }

        return TypedResults.Ok(visible.ToArray());
    }

    private static async Task<IResult> GetAsync([FromRoute] string id, HttpContext context, [FromServices] DesignedPageStore store)
    {
        if (await store.GetAsync(id) is not { } page || !await MayAsync(context, DesignedPagePermissions.Schema(page.Name)))
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(ToModel(page, await store.GetAsync(id, VersionOptions.Published) is not null));
    }

    private static async Task<IResult> CreateAsync([FromBody] DesignedPageWrite write, HttpContext context, CancellationToken cancellationToken) =>
        await MayAsync(context, DesignedPagePermissions.ManageDesignedPages) ? await SaveAsync(context, null, write, cancellationToken) : TypedResults.Forbid();

    private static async Task<IResult> UpdateAsync([FromRoute] string id, [FromBody] DesignedPageWrite write, HttpContext context, [FromServices] DesignedPageStore store, CancellationToken cancellationToken)
    {
        if (await store.GetAsync(id) is not { } existing)
        {
            return TypedResults.NotFound();
        }

        // The name keys the page's permissions: renaming it takes the shell-level set.
        if (!await MayAsync(context, DesignedPagePermissions.Design(existing.Name))
            || (!string.Equals(existing.Name, write.Name?.Trim(), StringComparison.Ordinal) && !await MayAsync(context, DesignedPagePermissions.ManageDesignedPages)))
        {
            return TypedResults.Forbid();
        }

        return await SaveAsync(context, id, write, cancellationToken);
    }

    private static async Task<IResult> PublishAsync([FromRoute] string id, HttpContext context, [FromServices] DesignedPageStore store, CancellationToken cancellationToken)
    {
        if (await store.GetAsync(id) is not { } page)
        {
            return TypedResults.NotFound();
        }

        if (!await MayAsync(context, DesignedPagePermissions.Publish(page.Name)))
        {
            return TypedResults.Forbid();
        }

        var problems = await ValidateAsync(context, ToPart(page), id, cancellationToken);
        if (problems.Count > 0)
        {
            return TypedResults.BadRequest(new DesignedPageValidation(false, problems));
        }

        await store.PublishAsync(id);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> DeleteAsync([FromRoute] string id, HttpContext context, [FromServices] DesignedPageStore store)
    {
        if (await store.GetAsync(id) is null)
        {
            return TypedResults.NotFound();
        }

        if (!await MayAsync(context, DesignedPagePermissions.ManageDesignedPages))
        {
            return TypedResults.Forbid();
        }

        await store.RemoveAsync(id);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> SaveAsync(HttpContext context, string? id, DesignedPageWrite write, CancellationToken cancellationToken)
    {
        var store = context.RequestServices.GetRequiredService<DesignedPageStore>();
        var part = write.ToPart();
        var problems = await ValidateAsync(context, part, id, cancellationToken);
        if (problems.Count > 0)
        {
            return TypedResults.BadRequest(new DesignedPageValidation(false, problems));
        }

        var saved = await store.SaveDraftAsync(id, part);
        return TypedResults.Ok(ToModel(saved, await store.GetAsync(saved.ContentItemId, VersionOptions.Published) is not null));
    }

    private static async Task<List<DisplayDiagnostic>> ValidateAsync(HttpContext context, DesignedPagePart part, string? id, CancellationToken cancellationToken)
    {
        var store = context.RequestServices.GetRequiredService<DesignedPageStore>();
        var validator = context.RequestServices.GetRequiredService<TemplateValidator>();
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

    private static async Task<bool> MayAsync(HttpContext context, Crest.Security.Permissions.Permission permission) =>
        await context.RequestServices.GetRequiredService<IAuthorizationService>().AuthorizeAsync(context.User, permission);

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
