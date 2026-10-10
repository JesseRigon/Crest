using Crest.Access;
using Crest.ContentManagement.Records;

namespace Crest.Services;

/// <summary>
/// Declares that a content type is scoped by ASSIGNMENT: users see only the items
/// assigned to them, on top of whatever the permission check already allowed.
/// </summary>
/// <remarks>
/// Registered per content type rather than inferred, because "this type is
/// assignment-scoped" is a policy decision. A type nobody declares keeps plain
/// ownership semantics.
/// </remarks>
public sealed class AssignmentScopedTypes
{
    private readonly Dictionary<string, string?> _kindsByType = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Scope <paramref name="contentType"/> to items assigned to the current user.
    /// <paramref name="kind"/> narrows to one assignment role; null accepts any.
    /// </summary>
    public AssignmentScopedTypes Add(string contentType, string? kind = null)
    {
        _kindsByType[contentType] = kind;
        return this;
    }

    public bool IsScoped(string contentType) => _kindsByType.ContainsKey(contentType);

    public string? KindFor(string contentType) =>
        _kindsByType.TryGetValue(contentType, out var kind) ? kind : null;

    public IEnumerable<string> Types => _kindsByType.Keys;
}

/// <summary>
/// Assignment narrows what the permission scope granted; it never widens it. For every
/// assignment-scoped type the caller may only see the items assigned to them; every other
/// type passes through to the content scope.
/// </summary>
public sealed class AssignmentScopeProvider(
    AssignmentScopedTypes scopedTypes,
    ICrestAssignmentService assignments) : IScopeProvider
{
    public string Target => nameof(ContentItemIndex);

    public int Version => 1;

    public async Task<ScopeRule> BuildAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        var scoped = scopedTypes.Types.ToArray();
        if (scoped.Length == 0 || caller.IsSuperUser)
        {
            return ScopeRule.All;
        }

        var passThrough = ScopeFilter.Of(ScopeCondition.NotIn(nameof(ContentItemIndex.ContentType), scoped.Cast<object?>()));

        if (caller.UserId is null)
        {
            return ScopeRule.Where(passThrough);
        }

        var branches = new List<ScopeFilter> { passThrough };
        foreach (var type in scoped)
        {
            var assigned = await assignments.FindAssignedIdsAsync(
                type,
                [new AssignmentRequirement(caller.UserId, scopedTypes.KindFor(type))],
                AssignmentMatch.Any,
                cancellationToken);

            // Exactly one requirement is always passed, so null ("unconstrained") cannot
            // happen; an EMPTY set means constrained and nothing matched: that type is gone.
            ArgumentNullException.ThrowIfNull(assigned);
            if (assigned.Count == 0)
            {
                continue;
            }

            branches.Add(ScopeFilter.AllOf(
                ScopeFilter.Of(ScopeCondition.Equal(nameof(ContentItemIndex.ContentType), type)),
                ScopeFilter.Of(ScopeCondition.In(nameof(ContentItemIndex.ContentItemId), assigned))));
        }

        return ScopeRule.Where(ScopeFilter.AnyOf([.. branches]));
    }
}
