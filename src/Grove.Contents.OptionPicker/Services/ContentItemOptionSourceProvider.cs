using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json.Dynamic;
using System.Text.Json.Nodes;
using Fluid;
using Fluid.Values;
using Grove.Contents;
using Grove.Display.Liquid;
using Grove.Models;
using Grove.Infrastructure.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Grove.Contents.Fields.Indexing.SQL;
using Grove.Contents.Management;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents.Management.Records;
using Grove.Access;
using Grove.Data.Scoping;
using YesSql;
using YesSql.Services;

namespace Grove.Services;

/// <summary>
/// Exposes content items as a picker source: of one type ("contentitem:Customer"), of several
/// types and stereotypes ("contentitem:Article,@Widget") or of every type without a stereotype
/// ("contentitem:*"); see <see cref="ContentItemSourceScope"/>. This is the source behind entity
/// dropdowns (customers, vendors, sites) and every content item reference, as opposed to the
/// fixed named choices an option list holds. Labels can come from a Liquid title pattern,
/// lookups carry edit links where the caller may edit, and the source offers a create link per
/// allowed creatable type the caller may create.
/// </summary>
/// <remarks>
/// <para>
/// The reason this exists separately from <see cref="UserOptionSourceProvider"/> is
/// PUSHDOWN. The user provider materializes its rows and matches them in memory, which
/// is fine for a tenant's user table and completely wrong for a customer table with a
/// million rows: "show only active customers" must not mean "load a million customers
/// and throw most of them away".
/// </para>
/// <para>
/// So filters here are split. Anything expressible against the content item index
/// (<see cref="OptionRowPaths"/>) becomes part of the SQL query. Anything addressing a
/// value inside the item's own document is matched afterwards, against the already
/// narrowed set. <see cref="Pushdown"/> is the pure part of that decision and is unit
/// tested directly, because a filter silently falling back to in-memory matching is
/// exactly the kind of regression that is invisible until a table gets large.
/// </para>
/// <para>
/// NOTE: none of this is authorization. These filters say what the DROPDOWN offers,
/// not what the USER may see - see the 6d data-scope work.
/// </para>
/// </remarks>
public sealed class ContentItemOptionSourceProvider(
    ISession session,
    ICallerContextAccessor callerAccessor,
    IScopeSetProvider scopes,
    IContentDefinitionManager contentDefinitionManager,
    IServiceProvider services) : IOptionSourceProvider, IOptionSourceCreateLinks
{
    /// <summary>The content types a qualifier names, in its order: the listed types, the types
    /// with a listed stereotype, or every type without a stereotype.</summary>
    private async Task<IReadOnlyList<string>> ResolveTypesAsync(string qualifier)
    {
        var scope = ContentItemSourceScope.Parse(qualifier);
        if (scope.IsEmpty)
        {
            return [];
        }

        if (scope.Stereotypes.Count == 0 && !scope.AllTypes)
        {
            return scope.Types;
        }

        var definitions = await contentDefinitionManager.ListTypeDefinitionsAsync();
        return [.. definitions
            .Where(type => scope.AllTypes
                ? !type.HasStereotype()
                : scope.Types.Contains(type.Name, StringComparer.Ordinal)
                    || (type.TryGetStereotype(out var stereotype) && scope.Stereotypes.Contains(stereotype, StringComparer.Ordinal)))
            .Select(type => type.Name)];
    }

    public async Task<IReadOnlyList<OptionCreateLink>> GetCreateLinksAsync(string qualifier, CancellationToken cancellationToken = default)
    {
        var user = services.GetService<Microsoft.AspNetCore.Http.IHttpContextAccessor>()?.HttpContext?.User;
        if (user is null)
        {
            return [];
        }

        var contentManager = services.GetRequiredService<IContentManager>();
        var authorization = services.GetRequiredService<IAuthorizationService>();
        var links = new List<OptionCreateLink>();
        foreach (var name in await ResolveTypesAsync(qualifier))
        {
            var type = await contentDefinitionManager.GetTypeDefinitionAsync(name);
            if (type is null || !type.IsCreatable())
            {
                continue;
            }

            var probe = await contentManager.NewAsync(type.Name);
            if (await authorization.AuthorizeAsync(user, CommonPermissions.EditContent, probe))
            {
                links.Add(new OptionCreateLink("New " + type.DisplayName, $"Contents/ContentTypes/{Uri.EscapeDataString(type.Name)}/Create"));
            }
        }

        return links;
    }

    /// <summary>The caller's content scope, from the one scope set: what this USER may see,
    /// as opposed to what the dropdown is configured to offer.</summary>
    private async Task<ScopeRule> ScopeAsync(CancellationToken cancellationToken)
    {
        var caller = callerAccessor.Current;
        // No caller means no grant. Failing closed here matters: an unauthenticated path that
        // fell through to "unrestricted" would expose every record.
        if (caller is null)
        {
            return ScopeRule.None;
        }

        return (await scopes.GetAsync(caller, cancellationToken)).Require(nameof(ContentItemIndex));
    }

    public const string ProviderKey = OptionSourceKeys.ContentItemProvider;

    public string Key => ProviderKey;

    /// <summary>Paths served straight from the content item index, so filters and
    /// sorts on them become part of the query rather than post-processing.</summary>
    public static class OptionRowPaths
    {
        public const string DisplayText = "DisplayText";
        public const string ContentType = "ContentType";
        public const string Owner = "Owner";
        public const string Author = "Author";
        public const string Published = "Published";
        public const string CreatedUtc = "CreatedUtc";
        public const string ModifiedUtc = "ModifiedUtc";

        /// <summary>Prefix for values read from the item's own document, e.g.
        /// "Field:CustomerPart.Status". Matched in memory after the query.</summary>
        public const string FieldPrefix = "Field:";

        public static bool IsIndexed(string path) => path is
            DisplayText or ContentType or Owner or Author or Published or CreatedUtc or ModifiedUtc;
    }

    public Task<IReadOnlyList<OptionSourceColumnDescriptor>> DescribeColumnsAsync(string qualifier, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<OptionSourceColumnDescriptor>>(
        [
            new(OptionRowPaths.DisplayText, "Name", IsDefaultDisplay: true),
            new(OptionRowPaths.Owner, "Owner"),
            new(OptionRowPaths.Published, "Published"),
            new(OptionRowPaths.CreatedUtc, "Created"),
            new(OptionRowPaths.ModifiedUtc, "Modified"),
        ]);

    public async Task<IReadOnlyList<OptionRow>> QueryAsync(OptionSourceQuery query, CancellationToken cancellationToken = default)
    {
        var types = await ResolveTypesAsync(query.Qualifier);
        if (types.Count == 0)
        {
            return [];
        }

        // Field: paths resolve against one type's definition; over several types they are
        // matched in memory.
        var single = types.Count == 1 ? types[0] : null;

        // Authorization BEFORE configuration: what this user may see bounds what the
        // dropdown may offer, never the other way round.
        var scope = await ScopeAsync(cancellationToken);
        if (scope.Kind == ScopeKind.None)
        {
            // Fail closed. Skipping the restriction here would hand every record to a
            // user entitled to none.
            return [];
        }

        var plan = Pushdown(query.Filters);

        // Index pushdown: resolve Field: paths (residual filter paths and sort
        // columns) against the type's definition, so sorts and filters on
        // Text/Numeric/Boolean fields can join the stock ContentFields.Indexing.SQL
        // tables instead of being applied after paging.
        var sortColumns = query.SortColumns ?? [];
        var targets = await ResolveFieldTargetsAsync(
            single,
            plan.Residual.Select(filter => filter.Path).Concat(sortColumns));

        var sortPlan = PlanSqlSort(sortColumns, targets);
        IReadOnlySet<FieldIndexKind> reservedKinds = sortPlan.FieldTarget is { } sortTarget
            ? new HashSet<FieldIndexKind> { sortTarget.Kind }
            : new HashSet<FieldIndexKind>();
        var promotion = PlanFieldFilterPromotion(plan.Residual, targets, reservedKinds);
        var residual = promotion.Remaining;

        var contentQuery = BuildQuery(types, plan.Indexed, query.SearchText, scope);
        var executable = ApplyPromotedFilters(
            ApplySqlSort(contentQuery, single ?? types[0], sortPlan),
            single ?? types[0],
            promotion.Promoted);

        var items = await executable
            // Only paging is deferred when residual filters exist: taking the page
            // before matching them would drop rows that should have been on it.
            .Skip(residual.Count == 0 ? query.Skip : 0)
            .Take(residual.Count == 0 ? query.Take : query.Take * PageOversampleFactor)
            .ListAsync();

        var titles = await RenderTitlesAsync(items, query.TitlePattern);
        var rows = items.Select(item => ToRow(item, query.Columns, titles));

        if (residual.Count > 0)
        {
            rows = OptionFilterMatcher.Apply(rows, residual);
            // The candidate set is materialized on this path, so the instance sort
            // can apply before the page is taken.
            rows = OptionRowSorter.Apply(rows, query.SortColumns);
            rows = rows.Skip(query.Skip).Take(query.Take);
        }
        else if (sortColumns.Count > sortPlan.PushedColumns)
        {
            // Whatever SQL could not express (an unresolvable column, or tiebreak
            // columns behind a field-index primary sort) refines within the page.
            // The primary cross-page order is SQL's wherever the plan pushed it.
            rows = OptionRowSorter.Apply(rows, query.SortColumns);
        }

        return [.. rows];
    }

    public Task<IReadOnlyList<OptionRow>> GetByIdsAsync(string qualifier, IReadOnlyList<string> ids, IReadOnlyList<string> columns, CancellationToken cancellationToken = default) =>
        GetByIdsAsync(new OptionSourceLookup(qualifier, ids, columns), cancellationToken);

    public async Task<IReadOnlyList<OptionRow>> GetByIdsAsync(OptionSourceLookup lookup, CancellationToken cancellationToken = default)
    {
        var ids = lookup.Ids;
        var types = ids.Count == 0 ? [] : await ResolveTypesAsync(lookup.Qualifier);
        if (types.Count == 0)
        {
            return [];
        }

        // Deliberately unfiltered by CURATION: a document written years ago references
        // whatever it referenced, and an inactive customer must still render on it.
        // Hidden means unselectable, not unresolvable.
        //
        // CONSTRAINED BY TYPE, though. Looking ids up without the content type would
        // let a caller resolve ids of ANY type through a source qualified as one -
        // scope is computed for the qualifier, so an unconstrained lookup would be
        // asking about one type while returning another.
        var scope = await ScopeAsync(cancellationToken);

        // The scope goes into the query: it may hold semi-joins (sharing), which only the
        // store evaluates.
        var items = await session
            .Query<ContentItem, ContentItemIndex>(index =>
                index.ContentItemId.IsIn(ids) && index.ContentType.IsIn(types) && index.Latest)
            .Where(ScopeExpressions.ToPredicate<ContentItemIndex>(scope))
            .ListAsync();

        // AUTHORIZATION is the exception, and this is where the bypass would otherwise
        // be: a user who cannot QUERY other people's records could enumerate them by
        // feeding ids to resolve.
        //
        // Out-of-scope ids are simply ABSENT from the result - not blanked in place.
        // A row the caller may not see must not come back as a hollow placeholder,
        // because a hollow row still asserts "this reference exists and resolves",
        // and a caller can act on that (totalling a list, counting matches) as though
        // the record were merely unnamed. Missing data is honest; fabricated data is
        // not. The referencing document is then itself in violation and its own
        // scope check drops it - see OptionSourceReferenceGuard.
        var titles = await RenderTitlesAsync(items, lookup.TitlePattern);
        var rows = new List<OptionRow>(items.Count());
        var user = lookup.IncludeEditUrls
            ? services.GetService<Microsoft.AspNetCore.Http.IHttpContextAccessor>()?.HttpContext?.User
            : null;
        var authorization = user is null ? null : services.GetRequiredService<IAuthorizationService>();

        // In the order the ids were asked for: a multiple selection is ordered.
        var byId = items.ToDictionary(item => item.ContentItemId, StringComparer.Ordinal);
        foreach (var id in ids.Distinct(StringComparer.Ordinal))
        {
            if (!byId.TryGetValue(id, out var item))
            {
                continue;
            }

            var row = ToRow(item, lookup.Columns, titles);
            if (authorization is not null && await authorization.AuthorizeAsync(user!, CommonPermissions.EditContent, item))
            {
                row = row with { EditUrl = $"Contents/ContentItems/{Uri.EscapeDataString(item.ContentItemId)}/Edit" };
            }

            rows.Add(row);
        }

        return rows;
    }

    /// <summary>Each item's label from the attachment's Liquid title pattern (in the item's
    /// culture, with <c>Model.ContentItem</c> and <c>ContentItem</c> bound), or none.</summary>
    internal async Task<IReadOnlyDictionary<string, string>?> RenderTitlesAsync(IEnumerable<ContentItem> items, string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern) || services.GetService<ILiquidTemplateManager>() is not { } liquid)
        {
            return null;
        }

        var titles = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            titles[item.ContentItemId] = (await liquid.RenderStringAsync(pattern, NullEncoder.Default, item,
                new Dictionary<string, FluidValue> { [nameof(ContentItem)] = new ObjectValue(item) })).Trim();
        }

        return titles;
    }

    // Residual filters are matched after the query, so the page has to be drawn from a
    // wider slice than the caller asked for. This bounds that widening; a source whose
    // residual filters reject more than this per page wants an index, not a bigger
    // multiplier.
    private const int PageOversampleFactor = 4;

    private IQuery<ContentItem, ContentItemIndex> BuildQuery(
        IReadOnlyList<string> contentTypes,
        IReadOnlyList<ResolvedOptionFilter> indexed,
        string? searchText,
        ScopeRule scope)
    {
        var query = session.Query<ContentItem, ContentItemIndex>(index =>
            index.ContentType.IsIn(contentTypes) && index.Latest);

        // The scope predicate is AND-ed on and cannot be opted out of by any attachment:
        // ownership, assignment and organization, in one rule, pushed into SQL.
        query = query.Where(ScopeExpressions.ToPredicate<ContentItemIndex>(scope));

        foreach (var filter in indexed)
        {
            var value = filter.Values.FirstOrDefault();

            // Each arm narrows the SQL rather than the materialized list - this is the
            // whole point of the provider.
            query = filter.Path switch
            {
                OptionRowPaths.DisplayText when filter.Operator == OptionFilterOperators.Contains =>
                    query.Where(index => index.DisplayText.Contains(value)),
                OptionRowPaths.DisplayText =>
                    query.Where(index => index.DisplayText == value),
                OptionRowPaths.Owner => query.Where(index => index.Owner == value),
                OptionRowPaths.Author => query.Where(index => index.Author == value),
                OptionRowPaths.Published => query.Where(index => index.Published == IsTrue(value)),
                OptionRowPaths.CreatedUtc when filter.Operator == OptionFilterOperators.GreaterThan =>
                    query.Where(index => index.CreatedUtc > AsDate(value)),
                OptionRowPaths.CreatedUtc when filter.Operator == OptionFilterOperators.LessThan =>
                    query.Where(index => index.CreatedUtc < AsDate(value)),
                OptionRowPaths.ModifiedUtc when filter.Operator == OptionFilterOperators.GreaterThan =>
                    query.Where(index => index.ModifiedUtc > AsDate(value)),
                OptionRowPaths.ModifiedUtc when filter.Operator == OptionFilterOperators.LessThan =>
                    query.Where(index => index.ModifiedUtc < AsDate(value)),
                _ => query,
            };
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var needle = searchText.Trim();
            query = query.Where(index => index.DisplayText.Contains(needle));
        }

        // Ordering is applied by ApplySqlSort, which needs the sort plan.
        return query;
    }

    /// <summary>
    /// Splits filters into those the index can answer and those that need the item's
    /// document. Pure and internal so the split is unit-testable: whether a filter
    /// reaches the database is a performance contract, not an implementation detail.
    /// </summary>
    internal static FilterPushdownPlan Pushdown(IReadOnlyList<ResolvedOptionFilter>? filters)
    {
        if (filters is null || filters.Count == 0)
        {
            return new FilterPushdownPlan([], []);
        }

        var indexed = new List<ResolvedOptionFilter>();
        var residual = new List<ResolvedOptionFilter>();

        foreach (var filter in filters)
        {
            // Multi-value comparisons and negation do not map onto the single-column
            // predicates above, so they stay in memory rather than being translated
            // into something subtly different.
            var translatable =
                OptionRowPaths.IsIndexed(filter.Path)
                && filter.Values.Count == 1
                && filter.Operator is not (OptionFilterOperators.NotEquals or OptionFilterOperators.In);

            (translatable ? indexed : residual).Add(filter);
        }

        return new FilterPushdownPlan(indexed, residual);
    }

    /// <summary>
    /// Resolves Field: paths against the type's definition: which part carries the
    /// field, and which stock field-index table (Text/Numeric/Boolean) holds its
    /// value. Paths accept "FieldSet.Field" or a bare "Field" (first match wins); a path
    /// that resolves to no field, or to a field type without a stock SQL index, maps
    /// to null and stays in-memory.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, FieldIndexTarget?>> ResolveFieldTargetsAsync(
        string? qualifier, IEnumerable<string> paths)
    {
        var results = new Dictionary<string, FieldIndexTarget?>(StringComparer.OrdinalIgnoreCase);
        Grove.Contents.Management.Metadata.Models.ContentTypeDefinition? type = null;
        var typeLoaded = false;

        foreach (var raw in paths)
        {
            if (string.IsNullOrWhiteSpace(raw) || results.ContainsKey(raw) || OptionRowPaths.IsIndexed(raw))
            {
                continue;
            }

            if (!typeLoaded)
            {
                type = qualifier is null ? null : await contentDefinitionManager.GetTypeDefinitionAsync(qualifier);
                typeLoaded = true;
            }

            results[raw] = type is null ? null : ResolveFieldTarget(type, raw);
        }

        return results;
    }

    internal static FieldIndexTarget? ResolveFieldTarget(
        Grove.Contents.Management.Metadata.Models.ContentTypeDefinition type, string path)
    {
        var trimmed = path.StartsWith(OptionRowPaths.FieldPrefix, StringComparison.OrdinalIgnoreCase)
            ? path[OptionRowPaths.FieldPrefix.Length..]
            : path;
        var segments = trimmed.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length is 0 or > 2)
        {
            return null;
        }

        foreach (var typeFieldSet in type.FieldSets)
        {
            foreach (var field in typeFieldSet.FieldSetDefinition.Fields)
            {
                var matches = segments.Length == 2
                    ? string.Equals(typeFieldSet.Name, segments[0], StringComparison.OrdinalIgnoreCase)
                        && string.Equals(field.Name, segments[1], StringComparison.OrdinalIgnoreCase)
                    : string.Equals(field.Name, segments[0], StringComparison.OrdinalIgnoreCase);
                if (!matches)
                {
                    continue;
                }

                // The index rows record the TYPE part's name (which is also the part
                // definition name for non-reusable parts) - see TextFieldIndexProvider.
                return field.FieldTypeDefinition.Name switch
                {
                    "TextField" => new FieldIndexTarget(typeFieldSet.Name, field.Name, FieldIndexKind.Text),
                    "NumericField" => new FieldIndexTarget(typeFieldSet.Name, field.Name, FieldIndexKind.Numeric),
                    "BooleanField" => new FieldIndexTarget(typeFieldSet.Name, field.Name, FieldIndexKind.Boolean),
                    _ => null,
                };
            }
        }

        return null;
    }

    /// <summary>
    /// Decides how much of the instance sort SQL can express. Pure and internal for
    /// the same reason as <see cref="Pushdown"/>: whether a sort reaches the database
    /// decides cross-page correctness. Three shapes, most preferred first:
    /// a chain of ContentItemIndex paths (a non-indexed tail cuts the chain there);
    /// or a single field-index primary sort when the FIRST column resolves to a
    /// Text/Numeric/Boolean field (one join per index type is the YesSql constraint,
    /// so tiebreak columns behind it refine in-page); or nothing (DisplayText
    /// default). NOTE: SQL collation, not the request culture, orders pushed sorts -
    /// cross-page correctness is worth that trade and it matches every stock Grove
    /// list.
    /// </summary>
    internal static SqlSortPlan PlanSqlSort(
        IReadOnlyList<string> sortColumns, IReadOnlyDictionary<string, FieldIndexTarget?> targets)
    {
        if (sortColumns.Count == 0)
        {
            return SqlSortPlan.None;
        }

        var indexedPrefix = sortColumns.TakeWhile(OptionRowPaths.IsIndexed).ToArray();
        if (indexedPrefix.Length > 0)
        {
            return new SqlSortPlan(indexedPrefix.Length, indexedPrefix, null);
        }

        if (targets.TryGetValue(sortColumns[0], out var target) && target is not null)
        {
            return new SqlSortPlan(1, [], target);
        }

        return SqlSortPlan.None;
    }

    /// <summary>
    /// Promotes residual NUMERIC and BOOLEAN field filters into field-index joins -
    /// their SQL comparison semantics match the in-memory matcher exactly, and
    /// promotion turns the residual path's bounded oversampling into an exact query.
    /// TEXT filters are deliberately NOT promoted: the in-memory matcher compares
    /// case-insensitively while SQL string equality follows the database collation
    /// (case-sensitive on SQLite), and a filter that silently changes semantics when
    /// it reaches SQL is worse than one that stays in memory. One join per index
    /// type (YesSql merges same-type joins), minus any type the sort reserved.
    /// </summary>
    internal static FieldFilterPromotion PlanFieldFilterPromotion(
        IReadOnlyList<ResolvedOptionFilter> residual,
        IReadOnlyDictionary<string, FieldIndexTarget?> targets,
        IReadOnlySet<FieldIndexKind> reservedKinds)
    {
        if (residual.Count == 0)
        {
            return new FieldFilterPromotion([], residual);
        }

        var usedKinds = new HashSet<FieldIndexKind>(reservedKinds);
        var promoted = new List<PromotedFieldFilter>();
        var remaining = new List<ResolvedOptionFilter>();

        foreach (var filter in residual)
        {
            var promotable = filter.Values.Count == 1
                && targets.TryGetValue(filter.Path, out var target)
                && target is not null
                && !usedKinds.Contains(target.Kind)
                && IsPromotableOperator(target.Kind, filter.Operator, filter.Values[0]);

            if (promotable)
            {
                var resolvedTarget = targets[filter.Path]!;
                promoted.Add(new PromotedFieldFilter(filter, resolvedTarget));
                usedKinds.Add(resolvedTarget.Kind);
            }
            else
            {
                remaining.Add(filter);
            }
        }

        return new FieldFilterPromotion(promoted, remaining);
    }

    private static bool IsPromotableOperator(FieldIndexKind kind, string @operator, string value) => kind switch
    {
        FieldIndexKind.Numeric => @operator is OptionFilterOperators.Equals
                or OptionFilterOperators.GreaterThan
                or OptionFilterOperators.LessThan
            && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _),
        FieldIndexKind.Boolean => @operator == OptionFilterOperators.Equals && bool.TryParse(value, out _),
        _ => false,
    };

    private static IQuery<ContentItem> ApplySqlSort(
        IQuery<ContentItem, ContentItemIndex> query, string qualifier, SqlSortPlan plan)
    {
        if (plan.FieldTarget is { } target)
        {
            // Joining the field index restricts results to items that HAVE a row -
            // items last saved before the field existed on the type drop out until
            // they are re-saved (or the index is rebuilt). Same caveat as every
            // stock field-index consumer.
            return target.Kind switch
            {
                FieldIndexKind.Numeric => query
                    .With<NumericFieldIndex>(index => index.ContentType == qualifier && index.Latest
                        && index.ContentFieldSet == target.FieldSetName && index.ContentField == target.FieldName)
                    .OrderBy(index => index.Numeric)
                    .ThenBy(index => index.ContentItemId),
                FieldIndexKind.Boolean => query
                    .With<BooleanFieldIndex>(index => index.ContentType == qualifier && index.Latest
                        && index.ContentFieldSet == target.FieldSetName && index.ContentField == target.FieldName)
                    .OrderBy(index => index.Boolean)
                    .ThenBy(index => index.ContentItemId),
                _ => query
                    .With<TextFieldIndex>(index => index.ContentType == qualifier && index.Latest
                        && index.ContentFieldSet == target.FieldSetName && index.ContentField == target.FieldName)
                    .OrderBy(index => index.Text)
                    .ThenBy(index => index.ContentItemId),
            };
        }

        if (plan.IndexedChain.Count > 0)
        {
            var ordered = OrderByIndexedPath(query, plan.IndexedChain[0], isFirst: true);
            for (var position = 1; position < plan.IndexedChain.Count; position++)
            {
                ordered = OrderByIndexedPath(ordered, plan.IndexedChain[position], isFirst: false);
            }

            // Deterministic paging even when the sorted values tie across a boundary.
            return ordered.ThenBy(index => index.ContentItemId);
        }

        return query.OrderBy(index => index.DisplayText);
    }

    private static IQuery<ContentItem, ContentItemIndex> OrderByIndexedPath(
        IQuery<ContentItem, ContentItemIndex> query, string path, bool isFirst) => path switch
    {
        OptionRowPaths.DisplayText => isFirst ? query.OrderBy(index => index.DisplayText) : query.ThenBy(index => index.DisplayText),
        OptionRowPaths.Owner => isFirst ? query.OrderBy(index => index.Owner) : query.ThenBy(index => index.Owner),
        OptionRowPaths.Author => isFirst ? query.OrderBy(index => index.Author) : query.ThenBy(index => index.Author),
        OptionRowPaths.Published => isFirst ? query.OrderBy(index => index.Published) : query.ThenBy(index => index.Published),
        OptionRowPaths.CreatedUtc => isFirst ? query.OrderBy(index => index.CreatedUtc) : query.ThenBy(index => index.CreatedUtc),
        OptionRowPaths.ModifiedUtc => isFirst ? query.OrderBy(index => index.ModifiedUtc) : query.ThenBy(index => index.ModifiedUtc),
        OptionRowPaths.ContentType => isFirst ? query.OrderBy(index => index.ContentType) : query.ThenBy(index => index.ContentType),
        _ => query,
    };

    private static IQuery<ContentItem> ApplyPromotedFilters(
        IQuery<ContentItem> query, string qualifier, IReadOnlyList<PromotedFieldFilter> promoted)
    {
        foreach (var (filter, target) in promoted)
        {
            switch (target.Kind)
            {
                case FieldIndexKind.Numeric:
                    var number = decimal.Parse(filter.Values[0], NumberStyles.Number, CultureInfo.InvariantCulture);
                    query = filter.Operator switch
                    {
                        OptionFilterOperators.GreaterThan => query.With<NumericFieldIndex>(index =>
                            index.ContentType == qualifier && index.Latest
                            && index.ContentFieldSet == target.FieldSetName && index.ContentField == target.FieldName
                            && index.Numeric > number),
                        OptionFilterOperators.LessThan => query.With<NumericFieldIndex>(index =>
                            index.ContentType == qualifier && index.Latest
                            && index.ContentFieldSet == target.FieldSetName && index.ContentField == target.FieldName
                            && index.Numeric < number),
                        _ => query.With<NumericFieldIndex>(index =>
                            index.ContentType == qualifier && index.Latest
                            && index.ContentFieldSet == target.FieldSetName && index.ContentField == target.FieldName
                            && index.Numeric == number),
                    };
                    break;
                case FieldIndexKind.Boolean:
                    var flag = bool.Parse(filter.Values[0]);
                    query = query.With<BooleanFieldIndex>(index =>
                        index.ContentType == qualifier && index.Latest
                        && index.ContentFieldSet == target.FieldSetName && index.ContentField == target.FieldName
                        && index.Boolean == flag);
                    break;
            }
        }

        return query;
    }

    private static bool IsTrue(string? value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1";

    private static DateTime AsDate(string? value) =>
        DateTime.TryParse(value, out var parsed) ? parsed.ToUniversalTime() : DateTime.MinValue;

    // Internal for tests: the projection is what a picker actually displays.
    internal static OptionRow ToRow(ContentItem item, IReadOnlyList<string> columns, IReadOnlyDictionary<string, string>? titles = null)
    {
        var requested = columns is { Count: > 0 } ? columns : [OptionRowPaths.DisplayText];
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        // ContentItem.Content is a dynamic view (JsonDynamicObject) over the item's
        // JSON. Convert once, through its implicit operator, rather than letting every
        // field read go through dynamic dispatch.
        JsonObject? document = item.Content is JsonDynamicObject dynamicObject
            ? (JsonObject)dynamicObject
            : item.Content as JsonObject;

        foreach (var column in requested)
        {
            values[column] = column switch
            {
                OptionRowPaths.DisplayText => titles?.GetValueOrDefault(item.ContentItemId) ?? item.DisplayText,
                OptionRowPaths.ContentType => item.ContentType,
                OptionRowPaths.Owner => item.Owner,
                OptionRowPaths.Author => item.Author,
                OptionRowPaths.Published => item.Published ? "true" : "false",
                OptionRowPaths.CreatedUtc => item.CreatedUtc?.ToString("o"),
                OptionRowPaths.ModifiedUtc => item.ModifiedUtc?.ToString("o"),
                _ when column.StartsWith(OptionRowPaths.FieldPrefix, StringComparison.OrdinalIgnoreCase) =>
                    ReadField(document, column[OptionRowPaths.FieldPrefix.Length..]),
                _ => ReadField(document, column),
            };
        }

        // A content item's identity IS its id, exactly as with users, so Key stays
        // null and the index records a null SelectedKey for this source.
        return new OptionRow(item.ContentItemId, values);
    }

    /// <summary>
    /// Walks a dotted path through the item's own document, unwrapping the common
    /// field shapes so "CustomerPart.Status" works whether Status is a text field, an
    /// option picker or a plain value.
    /// </summary>
    internal static string? ReadField(JsonNode? content, string path)
    {
        JsonNode? node = content;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (node is null)
            {
                return null;
            }

            node = node[segment];
        }

        if (node is null)
        {
            return null;
        }

        // An option picker holds SelectedIds - comparing against the first is what
        // makes "customers whose Type is the type chosen above" work.
        if (node["SelectedIds"] is JsonArray { Count: > 0 } selected)
        {
            return selected[0]?.ToString();
        }

        return (node["Text"] ?? node["Value"] ?? node)?.ToString();
    }
}

/// <summary>Which filters reached the database and which did not.</summary>
internal sealed record FilterPushdownPlan(
    IReadOnlyList<ResolvedOptionFilter> Indexed,
    IReadOnlyList<ResolvedOptionFilter> Residual);

/// <summary>Which stock field-index table serves a resolved Field: path.</summary>
internal enum FieldIndexKind
{
    Text,
    Numeric,
    Boolean,
}

/// <summary>A Field: path resolved against the type definition: the part carrying
/// the field, the field's name, and the field-index table holding its value.</summary>
internal sealed record FieldIndexTarget(string FieldSetName, string FieldName, FieldIndexKind Kind);

/// <summary>How much of the instance sort SQL expresses: a chain of
/// ContentItemIndex paths, OR one field-index primary sort, OR nothing (the
/// DisplayText default). <paramref name="PushedColumns"/> counts sort columns whose
/// cross-page order SQL owns; anything beyond it refines in-page.</summary>
internal sealed record SqlSortPlan(
    int PushedColumns,
    IReadOnlyList<string> IndexedChain,
    FieldIndexTarget? FieldTarget)
{
    public static readonly SqlSortPlan None = new(0, [], null);
}

/// <summary>A residual filter promoted into a field-index join.</summary>
internal sealed record PromotedFieldFilter(ResolvedOptionFilter Filter, FieldIndexTarget Target);

/// <summary>Residual filters split into promoted joins and what stays in memory.</summary>
internal sealed record FieldFilterPromotion(
    IReadOnlyList<PromotedFieldFilter> Promoted,
    IReadOnlyList<ResolvedOptionFilter> Remaining);
