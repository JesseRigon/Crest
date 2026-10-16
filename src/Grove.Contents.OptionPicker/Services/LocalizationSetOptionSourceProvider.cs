using Grove.Contents.Localization;
using Grove.Contents.Management;
using Grove.Contents.Management.Records;
using Grove.Models;
using Microsoft.AspNetCore.Authorization;
using YesSql;
using YesSql.Services;

namespace Grove.Services;

/// <summary>
/// Localization sets as an option source (<c>localization-set:{Type,Type}</c>, with Content
/// Localization): one row per set of the listed types, its id the set's, named by the set's item
/// in the current culture (else the default culture's, else the first). A field on this source
/// references "this page, in whatever culture is read" (docs/content-engine.md › Picking is one
/// field type). The caller sees the sets whose item it may view.
/// </summary>
public sealed class LocalizationSetOptionSourceProvider(
    ISession session,
    IServiceProvider services,
    IAuthorizationService authorizationService,
    Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor) : IOptionSourceProvider
{
    // Resolved when used: the content manager's handlers reach option sources.
    private IContentManager ContentManager => Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IContentManager>(services);

    private IContentLocalizationManager Localizations => Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IContentLocalizationManager>(services);

    // The candidates read per query: sets are deduplicated from items.
    private const int MaxItems = 200;

    public string Key => OptionSourceKeys.LocalizationSetProvider;

    public static class Columns
    {
        public const string DisplayText = "DisplayText";
        public const string ContentType = "ContentType";
        public const string Culture = "Culture";
    }

    public Task<IReadOnlyList<OptionSourceColumnDescriptor>> DescribeColumnsAsync(string qualifier, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<OptionSourceColumnDescriptor>>(
        [
            new(Columns.DisplayText, "Display text", IsDefaultDisplay: true),
            new(Columns.ContentType, "Content type"),
            new(Columns.Culture, "Culture"),
        ]);

    public async Task<IReadOnlyList<OptionRow>> QueryAsync(OptionSourceQuery query, CancellationToken cancellationToken = default)
    {
        var types = Types(query.Qualifier);
        if (types.Length == 0)
        {
            return [];
        }

        var items = await session.Query<ContentItem, ContentItemIndex>(index => index.ContentType.IsIn(types) && index.Latest)
            .OrderBy(index => index.DisplayText)
            .Take(MaxItems)
            .ListAsync(cancellationToken);

        IEnumerable<OptionRow> rows = await RowsAsync(await Localizations.DeduplicateContentItemsAsync(items), query.Columns);
        rows = OptionFilterMatcher.Apply(rows, query.Filters);
        rows = OptionFilterMatcher.Search(rows, query.SearchText, query.SearchColumns is { Count: > 0 } ? query.SearchColumns : [Columns.DisplayText, Columns.ContentType]);
        if (query.SortColumns is { Count: > 0 })
        {
            rows = OptionRowSorter.Apply(rows, query.SortColumns);
        }

        return [.. rows.Skip(query.Skip).Take(query.Take)];
    }

    public async Task<IReadOnlyList<OptionRow>> GetByIdsAsync(string qualifier, IReadOnlyList<string> ids, IReadOnlyList<string> columns, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var first = await Localizations.GetFirstItemIdForSetsAsync(ids);
        var items = (await ContentManager.GetAsync(first.Values, VersionOptions.Latest)).ToDictionary(item => item.ContentItemId, StringComparer.Ordinal);
        var sets = first.Where(pair => items.ContainsKey(pair.Value)).ToDictionary(pair => pair.Key, pair => items[pair.Value]);
        return await RowsAsync(sets, columns);
    }

    // The sets whose item the caller may view; a set it may not see is absent, never hollow.
    private async Task<List<OptionRow>> RowsAsync(IDictionary<string, ContentItem> sets, IReadOnlyList<string> columns)
    {
        var requested = columns is { Count: > 0 } ? columns : [Columns.DisplayText];
        var user = httpContextAccessor.HttpContext?.User;
        var rows = new List<OptionRow>();
        foreach (var (set, item) in sets)
        {
            if (user is null || !await authorizationService.AuthorizeAsync(user, Grove.Contents.CommonPermissions.ViewContent, item))
            {
                continue;
            }

            var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var column in requested)
            {
                values[column] = column switch
                {
                    Columns.DisplayText => item.DisplayText,
                    Columns.ContentType => item.ContentType,
                    Columns.Culture => ((System.Text.Json.Nodes.JsonObject)item.Content)["Localization"]?["Culture"]?["Text"]?.GetValue<string>(),
                    _ => null,
                };
            }

            rows.Add(new OptionRow(set, values) { Url = Grove.Contents.Fields.ViewModels.ContentItemLinks.SitePath(item) });
        }

        return rows;
    }

    private static string[] Types(string qualifier) =>
        (qualifier ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
