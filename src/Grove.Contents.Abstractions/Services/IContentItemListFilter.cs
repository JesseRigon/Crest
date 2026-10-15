#nullable enable

using Grove.ContentManagement;
using YesSql;

namespace Grove.Contents.Services;

/// <summary>
/// A filter a module adds to the content items list API (<c>api/grove/content-items</c>): it
/// describes itself (a query parameter and its choices, which the list page offers as a filter)
/// and narrows the list's query by the parameter's value.
/// </summary>
public interface IContentItemListFilter
{
    /// <summary>
    /// The filters this one offers (none when it has nothing to filter by), for the list of
    /// <paramref name="contentType"/>'s items when one is selected.
    /// </summary>
    Task<IReadOnlyList<ContentItemListFilterDescriptor>> DescribeAsync(string? contentType, CancellationToken cancellationToken = default);

    /// <summary>Narrows <paramref name="query"/> by the values of the parameters this filter describes.</summary>
    Task FilterAsync(IQuery<ContentItem> query, IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken = default);
}

/// <summary>
/// One filter of the content items list: the query parameter it reads, its label, its choices
/// (an empty value is "all"), and the content part whose items it concerns, if any (a
/// list shows that part's values in a column of its own).
/// </summary>
public sealed record ContentItemListFilterDescriptor(string Name, string Label, IReadOnlyList<ContentItemListFilterOption> Options, string? Part = null);

/// <summary>A filter choice: the parameter value and its text, with its nesting level in a tree (terms).</summary>
public sealed record ContentItemListFilterOption(string Value, string Text, int Level = 0);
