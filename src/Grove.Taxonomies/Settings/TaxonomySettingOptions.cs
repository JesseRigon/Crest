using Grove.ContentManagement;
using Grove.ContentManagement.Records;
using Grove.Settings;
using YesSql;

namespace Grove.Taxonomies.Settings;

/// <summary>
/// The published taxonomies, by content item id: the choices of a settings field that lists taxonomies.
/// </summary>
public sealed class TaxonomySettingOptions(YesSql.ISession session) : ISettingOptions
{
    public async Task<IReadOnlyList<SettingOption>> GetOptionsAsync(CancellationToken cancellationToken = default)
    {
        var taxonomies = await session.Query<ContentItem, ContentItemIndex>(index => index.ContentType == "Taxonomy" && index.Published).ListAsync(cancellationToken);

        return taxonomies
            .Select(taxonomy => new SettingOption(taxonomy.ContentItemId, taxonomy.DisplayText ?? taxonomy.ContentItemId))
            .ToArray();
    }
}
