using Grove.Infrastructure.Settings;

namespace Grove.Services;

/// <summary>
/// A source an option picker field can draw from. Providers
/// keep the field itself ignorant of users, items or content part lists, the same way
/// Grove.Display.Icons hosts icon providers.
/// </summary>
public interface IOptionSourceProvider
{
    /// <summary>Provider key matched against the first segment of a source key
    /// ("contentpartlist" in "contentpartlist:pricing.side").</summary>
    string Key { get; }

    /// <summary>
    /// The columns this source can offer for the given qualifier - drives the column
    /// picker in the field's settings editor, so paths never have to be typed blind.
    /// </summary>
    Task<IReadOnlyList<OptionSourceColumnDescriptor>> DescribeColumnsAsync(string qualifier, CancellationToken cancellationToken = default);

    /// <summary>Rows to offer in the dropdown, filtered and paged.</summary>
    Task<IReadOnlyList<OptionRow>> QueryAsync(OptionSourceQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rows for already-selected ids, so stored values can be rendered. BATCHED by
    /// design: rendering a 500-line document must be one call per source, not one per
    /// line.
    /// </summary>
    Task<IReadOnlyList<OptionRow>> GetByIdsAsync(string qualifier, IReadOnlyList<string> ids, IReadOnlyList<string> columns, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rows for already-selected ids, with what the attachment asks of them (a label pattern,
    /// edit links). Sources that support neither answer as <see cref="GetByIdsAsync(string, IReadOnlyList{string}, IReadOnlyList{string}, CancellationToken)"/>.
    /// </summary>
    Task<IReadOnlyList<OptionRow>> GetByIdsAsync(OptionSourceLookup lookup, CancellationToken cancellationToken = default) =>
        GetByIdsAsync(lookup.Qualifier, lookup.Ids, lookup.Columns, cancellationToken);
}

/// <summary>
/// A source whose records can be created from the picker: links to create one, limited to what
/// the caller may create (a content item source: one per allowed creatable type).
/// </summary>
public interface IOptionSourceCreateLinks
{
    Task<IReadOnlyList<OptionCreateLink>> GetCreateLinksAsync(string qualifier, CancellationToken cancellationToken = default);
}

/// <summary>A link to create a record of a source.</summary>
public sealed record OptionCreateLink(string Label, string Url);

/// <summary>A batched lookup of selected ids.</summary>
/// <param name="TitlePattern">The attachment's Liquid label pattern, for sources that render
/// one (content items); null for the source's own label.</param>
/// <param name="IncludeEditUrls">Whether rows carry <see cref="OptionRow.EditUrl"/> where the
/// caller may edit the record.</param>
public sealed record OptionSourceLookup(
    string Qualifier,
    IReadOnlyList<string> Ids,
    IReadOnlyList<string> Columns,
    string? TitlePattern = null,
    bool IncludeEditUrls = false);

/// <summary>What a provider can display for a source.</summary>
public sealed record OptionSourceColumnDescriptor(string Path, string Label, bool IsDefaultDisplay = false);

/// <summary>One row of a source: an id plus the requested columns.</summary>
public sealed record OptionRow(string Id, IReadOnlyDictionary<string, string?> Values)
{
    /// <summary>The technical key when the source has one distinct from the id
    /// (Content Part Lists do; entity sources like users do not - their id IS the key).</summary>
    public string? Key { get; init; }

    /// <summary>Set for options a tenant hid: still resolvable for history, but not
    /// offered in pickers.</summary>
    public bool Hidden { get; init; }

    /// <summary>Where the record is edited, set on lookups that ask for it when the source has
    /// an editor and the caller may use it (a content item the user may edit).</summary>
    public string? EditUrl { get; init; }

    // NOTE: there is deliberately no "redacted" row shape. A record the caller may not
    // see is ABSENT from the result rather than returned hollow - a placeholder row
    // still asserts that the reference resolves, which a caller can act on. The
    // referencing item is then out of scope too (OptionSourceReferenceGuard).
}

/// <summary>A dropdown query: what to search, restrict, sort and return.</summary>
public sealed record OptionSourceQuery(
    string Qualifier,
    IReadOnlyList<string> Columns,
    string? SearchText = null,
    IReadOnlyList<ResolvedOptionFilter>? Filters = null,
    IReadOnlyList<string>? SearchColumns = null,
    IReadOnlyList<string>? SortColumns = null,
    int Skip = 0,
    int Take = 50,
    string? TitlePattern = null);

/// <summary>
/// A filter with its comparison value already resolved - literal values pass straight
/// through, while <see cref="OptionFilter.ValueFrom"/> has been read from the item
/// being edited before the provider ever sees it.
/// </summary>
public sealed record ResolvedOptionFilter(string Path, string Operator, IReadOnlyList<string> Values)
{
    public static ResolvedOptionFilter From(OptionFilter filter, IReadOnlyList<string> values) =>
        new(filter.Path, filter.Operator, values);
}
