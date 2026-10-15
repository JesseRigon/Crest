#nullable enable

using System.Text.Json;
using System.Text.Json.Nodes;
using Grove.Api;
using Grove.Contents.Management;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents;
using Grove.Contents.Taxonomies.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using YesSql;

namespace Grove.Contents.Taxonomies.Api;

/// <summary>
/// A taxonomy's terms for the editors (<c>api/grove/taxonomies/{taxonomyId}</c>): the term tree
/// a taxonomy field picks from (<c>TaxonomyFieldEditor</c>), and the creation of a tag, a new
/// root term a tags editor adds by name. A taxonomy's own terms are edited in its native editor
/// (<c>TaxonomyPartEditor</c>), saved with the taxonomy through the content items API.
/// </summary>
public sealed class TaxonomiesApi : ApiContributor
{
    public override string Name => "Taxonomies";

    public override string FeatureId => "Grove.Contents.Taxonomies";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("taxonomies/{taxonomyId}");
        routes.MapGet("terms", (HttpContext __http, [FromRoute] string taxonomyId) => ApiHandlers.For<TaxonomiesHandlers>(__http).TermsAsync(taxonomyId));
        routes.MapPost("tags", (HttpContext __http, [FromRoute] string taxonomyId, [FromBody] CreateTagRequest request) => ApiHandlers.For<TaxonomiesHandlers>(__http).CreateTagAsync(taxonomyId, request));
    }
}

/// <summary>A term of the tree: its id, its name, and its children.</summary>
public sealed record TaxonomyTerm(string ContentItemId, string DisplayText, IReadOnlyList<TaxonomyTerm> Children);

/// <summary>A taxonomy's terms and the type new terms are created as.</summary>
public sealed record TaxonomyTerms(string ContentItemId, string? DisplayText, string? TermContentType, IReadOnlyList<TaxonomyTerm> Terms);

/// <summary>The name of the tag to add to the taxonomy.</summary>
public sealed record CreateTagRequest(string? DisplayText);

public sealed class TaxonomiesHandlers(
    IContentManager contentManager,
    IContentDefinitionManager definitions,
    IAuthorizationService authorizationService,
    YesSql.ISession session) : ApiHandlers
{
    public async Task<IResult> TermsAsync(string taxonomyId)
    {
        var taxonomy = await contentManager.GetAsync(taxonomyId, VersionOptions.Latest);
        if (taxonomy is null || !taxonomy.TryGet<TaxonomyPart>(out var part))
        {
            return NotFound();
        }

        // Whoever may see the taxonomy may pick its terms.
        if (!await authorizationService.AuthorizeAsync(User, CommonPermissions.ViewContent, taxonomy))
        {
            return Forbid();
        }

        return Ok(new TaxonomyTerms(taxonomy.ContentItemId, taxonomy.DisplayText, part.TermContentType, Tree(part.Terms)));
    }

    public async Task<IResult> CreateTagAsync(string taxonomyId, [FromBody] CreateTagRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayText))
        {
            return ApiProblems.Validation(nameof(CreateTagRequest.DisplayText), "The tag's name is required.");
        }

        if (!await authorizationService.AuthorizeAsync(User, Permissions.ManageTaxonomies))
        {
            return Forbid();
        }

        var taxonomyType = await definitions.GetTypeDefinitionAsync("Taxonomy");
        var draftable = taxonomyType?.IsDraftable() == true;
        var taxonomy = await contentManager.GetAsync(taxonomyId, draftable ? VersionOptions.DraftRequired : VersionOptions.Latest);
        if (taxonomy is null || !taxonomy.TryGet<TaxonomyPart>(out var part) || string.IsNullOrEmpty(part.TermContentType))
        {
            return NotFound();
        }

        // The tag term runs the content handlers (its route, its title), not an editor.
        var term = await contentManager.NewAsync(part.TermContentType);
        term.DisplayText = request.DisplayText.Trim();
        term.Weld<TermPart>();
        term.Alter<TermPart>(termPart => termPart.TaxonomyContentItemId = taxonomyId);

        var validation = await contentManager.ValidateAsync(term);
        if (!validation.Succeeded)
        {
            await session.CancelAsync();
            return ApiProblems.Validation(validation.Errors
                .GroupBy(error => error.MemberNames?.FirstOrDefault() ?? string.Empty)
                .ToDictionary(errors => errors.Key, errors => errors.Select(error => error.ErrorMessage ?? string.Empty).ToArray()));
        }

        await contentManager.CreateAsync(term, VersionOptions.Draft);

        // Tags are added at the root of the taxonomy; a draftable taxonomy is published with them.
        taxonomy.Alter<TaxonomyPart>(taxonomyPart => taxonomyPart.Terms.Add(term));
        if (draftable)
        {
            await contentManager.PublishAsync(taxonomy);
        }
        else
        {
            await session.SaveAsync(taxonomy);
        }

        return Ok(new TaxonomyTerm(term.ContentItemId, term.DisplayText, []));
    }

    private static IReadOnlyList<TaxonomyTerm> Tree(IEnumerable<ContentItem>? terms) =>
        [.. (terms ?? []).Select(term =>
        {
            JsonObject content = term.Content;
            var children = content["Terms"] is JsonArray array ? array.Deserialize<List<ContentItem>>() : null;
            return new TaxonomyTerm(term.ContentItemId, term.DisplayText ?? term.ContentItemId, Tree(children));
        })];
}
