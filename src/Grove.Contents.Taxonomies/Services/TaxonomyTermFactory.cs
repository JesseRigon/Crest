using Grove.Contents.Management;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents.Taxonomies.Models;
using Grove.Data.Documents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Grove.Contents.Taxonomies.Services;

/// <summary>
/// A tag added by name from an option list field on a taxonomy's terms
/// (<c>embedded:Taxonomy.Terms</c>, <c>AllowCreate</c>): a new root term of the taxonomy's term
/// type, validated as that type, saved with the taxonomy (published with it when the taxonomy is
/// draftable). Adding terms takes <c>ManageTaxonomies</c>.
/// </summary>
public sealed class TaxonomyTermFactory(
    IContentManager contentManager,
    IContentDefinitionManager definitions,
    IAuthorizationService authorizationService,
    IHttpContextAccessor httpContextAccessor,
    IDocumentStore documentStore) : IEmbeddedContentItemFactory
{
    public string ContentType => "Taxonomy";

    public string Embedding => TaxonomyFieldSet.TermsEmbedding;

    public async Task<EmbeddedContentItemCreation> CreateAsync(string parentContentItemId, string displayText, CancellationToken cancellationToken = default)
    {
        if (httpContextAccessor.HttpContext?.User is not { } user || !await authorizationService.AuthorizeAsync(user, Permissions.ManageTaxonomies))
        {
            return new(null, [], Forbidden: true);
        }

        var taxonomyType = await definitions.GetTypeDefinitionAsync(ContentType);
        var draftable = taxonomyType?.IsDraftable() == true;
        var taxonomy = await contentManager.GetAsync(parentContentItemId, draftable ? VersionOptions.DraftRequired : VersionOptions.Latest);
        if (taxonomy is null || !taxonomy.TryGet<TaxonomyFieldSet>(out var part) || string.IsNullOrEmpty(part.TermContentType))
        {
            return new(null, ["These categories do not take new terms."]);
        }

        // The tag term is an embedded item of the taxonomy, validated as its type, not an editor's.
        var term = await contentManager.NewAsync(part.TermContentType);
        term.DisplayText = displayText;
        term.Weld<TermFieldSet>();
        term.Alter<TermFieldSet>(termFieldSet => termFieldSet.TaxonomyContentItemId = parentContentItemId);

        var validation = await contentManager.ValidateAsync(term);
        if (!validation.Succeeded)
        {
            await documentStore.CancelAsync();
            return new(null, [.. validation.Errors.Select(error => error.ErrorMessage ?? string.Empty)]);
        }

        // Tags are added at the root of the taxonomy; a draftable taxonomy is published with them.
        taxonomy.AlterEmbedded(TaxonomyFieldSet.TermsEmbedding, terms => terms.ContentItems.Add(term));
        await contentManager.UpdateAsync(taxonomy);
        if (draftable)
        {
            await contentManager.PublishAsync(taxonomy);
        }

        return new(term, []);
    }
}
