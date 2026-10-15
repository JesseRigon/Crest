#nullable enable
using System.Text.Json;
using System.Text.Json.Nodes;
using Grove.Access;
using Grove.ContentManagement;
using Grove.ContentManagement.Routing;
using Grove.Localization;
using Grove.Queries.Structured;
using Grove.Settings;

namespace Grove.Queries.Builtin;

/// <summary>
/// The in-process source of the three fixed queries every page binds first: <c>item</c> (a
/// content item by id, or by path through Autoroute when it is available), <c>user</c> (the
/// current caller) and <c>site</c> (the site settings). Results are memoised per request.
/// </summary>
public sealed class SystemQuerySource : IQuerySource, IQueryDescriber
{
    public const string SourceName = "System";

    public const string ItemQuery = "item";
    public const string UserQuery = "user";
    public const string SiteQuery = "site";

    private static readonly QueryColumn[] s_itemColumns =
    [
        new("ContentItemId", typeof(string)),
        new("ContentItemVersionId", typeof(string)),
        new("ContentType", typeof(string)),
        new("DisplayText", typeof(string)),
        new("Owner", typeof(string)),
        new("Author", typeof(string)),
        new("Published", typeof(bool)),
        new("Latest", typeof(bool)),
        new("CreatedUtc", typeof(DateTime?)),
        new("ModifiedUtc", typeof(DateTime?)),
        new("PublishedUtc", typeof(DateTime?)),
    ];

    private static readonly QueryColumn[] s_userColumns =
    [
        new("Id", typeof(string)),
        new("Name", typeof(string)),
        new("Roles", typeof(string[])),
        new("Side", typeof(string)),
        new("OrganizationId", typeof(string)),
        new("IsAuthenticated", typeof(bool)),
    ];

    // Name and Branding are the anonymous subset (docs/branding.md › Permissions): the login
    // page renders them. The rest needs an authenticated caller.
    private static readonly QueryColumn[] s_siteColumns =
    [
        new("Name", typeof(string)),
        new("Branding", typeof(JsonObject)),
        new("DefaultCulture", typeof(string)),
        new("BaseUrl", typeof(string)),
    ];

    private static readonly QueryDescriptor[] s_descriptors =
    [
        new(ItemQuery, "Item", [new QueryParameter("id", QueryParameterType.String), new QueryParameter("path", QueryParameterType.String)], s_itemColumns, SourceName),
        new(UserQuery, "User", [], s_userColumns, SourceName),
        new(SiteQuery, "Site", [], s_siteColumns, SourceName),
    ];

    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, IQueryResults> _memo = new(StringComparer.Ordinal);

    public SystemQuerySource(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public string Name => SourceName;

    public string Source => SourceName;

    public IReadOnlyList<QueryDescriptor> BuiltInQueries => s_descriptors;

    public QueryDescriptor? Describe(Query query) =>
        s_descriptors.FirstOrDefault(descriptor => string.Equals(descriptor.Name, query.Name, StringComparison.OrdinalIgnoreCase));

    public async Task<IQueryResults> ExecuteQueryAsync(Query query, QueryRequest request)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(request);

        var key = query.Name.ToLowerInvariant() + "|" + (query.ReturnContentItems ? "items" : "rows") + "|" + JsonSerializer.Serialize(request.Parameters.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToDictionary(pair => pair.Key, pair => pair.Value?.ToString()));

        if (_memo.TryGetValue(key, out var memoised))
        {
            return memoised;
        }

        var results = query.Name.ToLowerInvariant() switch
        {
            ItemQuery => await ItemAsync(query, request),
            UserQuery => User(),
            SiteQuery => await SiteAsync(),
            _ => throw new InvalidOperationException($"The System source has no query named '{query.Name}'."),
        };

        _memo[key] = results;

        return results;
    }

    private async Task<IQueryResults> ItemAsync(Query query, QueryRequest request)
    {
        var id = Parameter(request, "id");
        var path = Parameter(request, "path");

        if (id is null && path is not null && _serviceProvider.GetService(typeof(IAutorouteEntries)) is IAutorouteEntries autoroute)
        {
            var (found, entry) = await autoroute.TryGetEntryByPathAsync(path.StartsWith('/') ? path : "/" + path);

            if (found)
            {
                id = entry.ContentItemId;
            }
        }

        // A path that Autoroute does not resolve names no item; it is never tried as an id.
        if (string.IsNullOrEmpty(id) || _serviceProvider.GetService(typeof(IContentManager)) is not IContentManager contentManager)
        {
            return QueryResults.Empty(s_itemColumns);
        }

        // The caller's scope is settled before anything is loaded: a caller with no scope (or
        // none of the content index) is refused without touching the item.
        var scopes = await CallerScopes.RequireAsync(
            _serviceProvider.GetService(typeof(ICallerContextAccessor)) as ICallerContextAccessor,
            _serviceProvider.GetService(typeof(IScopeSetProvider)) as IScopeSetProvider,
            QueryPlan.ContentIndexName,
            request.CancellationToken);

        var rule = scopes.Require(QueryPlan.ContentIndexName);

        if (rule.Kind == ScopeKind.None)
        {
            return QueryResults.Empty(s_itemColumns);
        }

        var item = await contentManager.GetAsync(id);

        if (item is null)
        {
            return QueryResults.Empty(s_itemColumns);
        }

        if (scopes.TryGet(item.ContentType, out var typeRule))
        {
            rule = rule.And(typeRule);
        }

        // A semi-join (sharing's scope) needs the store: the item is checked with the query.
        if (rule.Filter?.HasSelect == true)
        {
            if (_serviceProvider.GetService(typeof(YesSql.ISession)) is not YesSql.ISession session
                || (await ScopedContentItems.LoadVersionsAsync(session, [item.ContentItemVersionId], rule, request.CancellationToken)).Count == 0)
            {
                return QueryResults.Empty(s_itemColumns);
            }
        }
        else if (!rule.Admits(column => ItemColumn(item, column)))
        {
            return QueryResults.Empty(s_itemColumns);
        }

        return new QueryResults
        {
            Items = query.ReturnContentItems ? [item] : [Row(s_itemColumns, column => ItemColumn(item, column))],
            Columns = s_itemColumns,
            Total = 1,
        };
    }

    private IQueryResults User()
    {
        var caller = (_serviceProvider.GetService(typeof(ICallerContextAccessor)) as ICallerContextAccessor)?.Current;

        if (caller is null)
        {
            return QueryResults.Empty(s_userColumns);
        }

        var row = new JsonObject
        {
            ["Id"] = caller.UserId,
            ["Name"] = caller.UserName,
            ["Roles"] = new JsonArray(caller.Roles.OrderBy(role => role, StringComparer.OrdinalIgnoreCase).Select(role => (JsonNode?)JsonValue.Create(role)).ToArray()),
            ["Side"] = caller.Side.ToString(),
            ["OrganizationId"] = caller.OrganizationId,
            ["IsAuthenticated"] = caller.IsAuthenticated,
        };

        return new QueryResults { Items = [row], Columns = s_userColumns, Total = 1 };
    }

    private async Task<IQueryResults> SiteAsync()
    {
        if (_serviceProvider.GetService(typeof(ISiteService)) is not ISiteService siteService)
        {
            return QueryResults.Empty(s_siteColumns);
        }

        var site = await siteService.GetSiteSettingsAsync();

        // One entry per registered rendition: its URL, or "" when not set (empty is a value;
        // the surface falls back).
        var branding = new JsonObject();
        if (_serviceProvider.GetService(typeof(Microsoft.Extensions.Options.IOptions<BrandingOptions>)) is Microsoft.Extensions.Options.IOptions<BrandingOptions> brandingOptions)
        {
            foreach (var (name, url) in site.ResolveRenditions(brandingOptions.Value, _serviceProvider.GetService(typeof(IBrandingUrlResolver)) as IBrandingUrlResolver))
            {
                branding[name] = url;
            }
        }

        var row = new JsonObject
        {
            ["Name"] = site.SiteName,
            ["Branding"] = branding,
        };

        var caller = (_serviceProvider.GetService(typeof(ICallerContextAccessor)) as ICallerContextAccessor)?.Current;
        if (caller?.IsAuthenticated == true)
        {
            row["DefaultCulture"] = _serviceProvider.GetService(typeof(ILocalizationService)) is ILocalizationService localization
                ? await localization.GetDefaultCultureAsync()
                : null;
            row["BaseUrl"] = site.BaseUrl;
        }

        return new QueryResults { Items = [row], Columns = s_siteColumns, Total = 1 };
    }

    private static string? Parameter(QueryRequest request, string name)
    {
        var match = request.Parameters.FirstOrDefault(pair => string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase));

        return match.Key is null ? null : match.Value?.ToString();
    }

    private static object? ItemColumn(ContentItem item, string column) => column.ToLowerInvariant() switch
    {
        "contentitemid" => item.ContentItemId,
        "contentitemversionid" => item.ContentItemVersionId,
        "contenttype" => item.ContentType,
        "displaytext" => item.DisplayText,
        "owner" => item.Owner,
        "author" => item.Author,
        "published" => item.Published,
        "latest" => item.Latest,
        "createdutc" => item.CreatedUtc,
        "modifiedutc" => item.ModifiedUtc,
        "publishedutc" => item.PublishedUtc,
        _ => null,
    };

    private static JsonObject Row(IReadOnlyList<QueryColumn> columns, Func<string, object?> value)
    {
        var row = new JsonObject();

        foreach (var column in columns)
        {
            var v = value(column.Name);
            row[column.Name] = v is null ? null : JsonValue.Create(v);
        }

        return row;
    }
}
