using YesSql.Indexes;

namespace Grove.Sharing.Indexes;

/// <summary>
/// The access index (requirement 75): one row per object, principal and window of time, with
/// the principal's winning decision. List scopes select from it (semi-joins on
/// <see cref="ObjectId"/>); a row counts while <see cref="StartsUtc"/> ≤ now &lt;
/// <see cref="EndsUtc"/>, so access ends on time between recalculations (requirement 79).
/// </summary>
public sealed class ShareAccessIndex : MapIndex
{
    public string Kind { get; set; } = string.Empty;

    public string ObjectId { get; set; } = string.Empty;

    /// <summary>The object and the path (<see cref="ShareAccessDocument.AccessKey"/>): what the
    /// list scopes correlate one path's rows by.</summary>
    public string AccessKey { get; set; } = string.Empty;

    /// <summary>The <see cref="SharePrincipalKind"/>, which is also the tier.</summary>
    public int PrincipalKind { get; set; }

    /// <summary>The principal's id; a role's in lower case (role names match without case);
    /// empty for Anyone and Tenant.</summary>
    public string PrincipalId { get; set; } = string.Empty;

    public int Depth { get; set; }

    /// <summary>The <see cref="AccessLevel"/>; 0 for a block.</summary>
    public int Level { get; set; }

    public bool IsBlock { get; set; }

    /// <summary>The decision is on the object itself, not inherited.</summary>
    public bool IsDirect { get; set; }

    public DateTime? StartsUtc { get; set; }

    public DateTime? EndsUtc { get; set; }

    /// <summary>The object and the depth: what a <see cref="ShareRoleShadowIndex"/> row names.</summary>
    public string DepthKey { get; set; } = string.Empty;
}

/// <summary>
/// A role's row deeper than higher role grants at one depth on one object: for a caller holding
/// the role, those grants count only up to the row's level (requirement 60, the deepest
/// decision wins within the role tier).
/// </summary>
public sealed class ShareRoleShadowIndex : MapIndex
{
    public string Kind { get; set; } = string.Empty;

    public string ObjectId { get; set; } = string.Empty;

    /// <summary>The denied role, in lower case.</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>The <see cref="ShareAccessIndex.DepthKey"/> of the grants it outranks.</summary>
    public string DepthKey { get; set; } = string.Empty;

    /// <summary>The deeper row's level (0 for a block): it cancels the grants for any
    /// threshold above it.</summary>
    public int Level { get; set; }

    public DateTime? StartsUtc { get; set; }

    public DateTime? EndsUtc { get; set; }
}

/// <summary>
/// One row per calculated object: finding its document, and the time registry (requirement
/// 79): <see cref="NextBoundaryUtc"/> is when a decision on its chain next starts or ends, and
/// the job reads only the rows now due.
/// </summary>
public sealed class ShareAccessObjectIndex : MapIndex
{
    public string Kind { get; set; } = string.Empty;

    public string ObjectId { get; set; } = string.Empty;

    /// <summary>The object and the path (<see cref="ShareAccessDocument.AccessKey"/>).</summary>
    public string AccessKey { get; set; } = string.Empty;

    public bool IsPublic { get; set; }

    /// <summary>Shared with someone other than its owner (the access indicator).</summary>
    public bool IsShared { get; set; }

    public DateTime? NextBoundaryUtc { get; set; }
}

/// <summary>The subtrees waiting for a background recalculation.</summary>
public sealed class ShareRecalculationIndex : MapIndex
{
    public string Kind { get; set; } = string.Empty;

    public string ObjectId { get; set; } = string.Empty;

    public DateTime RequestedUtc { get; set; }
}

public sealed class ShareAccessIndexProvider : IndexProvider<ShareAccessDocument>
{
    /// <summary>The key a role grant row and the shadows over it share: the object, the path
    /// (an access key) and a depth.</summary>
    public static string DepthKey(string kind, string accessKey, int depth) => $"{kind}:{accessKey}#{depth}";

    /// <summary>A principal id as the index stores it: roles in lower case (role names match
    /// without case), an organization-scoped role as <c>role@org:id</c>; empty for Anyone and
    /// Tenant.</summary>
    public static string IndexedId(SharePrincipal principal) =>
        principal.Kind != SharePrincipalKind.Role
            ? principal.Id ?? string.Empty
            : principal.OrganizationId is null
                ? (principal.Id ?? string.Empty).ToLowerInvariant()
                : $"{(principal.Id ?? string.Empty).ToLowerInvariant()}@org:{principal.OrganizationId}";

    public override void Describe(DescribeContext<ShareAccessDocument> context)
    {
        context.For<ShareAccessObjectIndex>()
            .Map(document => new ShareAccessObjectIndex
            {
                Kind = document.Kind,
                ObjectId = document.ObjectId,
                AccessKey = document.AccessKey,
                IsPublic = document.IsPublic,
                IsShared = document.IsShared,
                NextBoundaryUtc = document.NextBoundaryUtc,
            });

        context.For<ShareAccessIndex>()
            .Map(document => document.Rows.Select(row => new ShareAccessIndex
            {
                Kind = document.Kind,
                ObjectId = document.ObjectId,
                AccessKey = document.AccessKey,
                PrincipalKind = (int)row.PrincipalKind,
                PrincipalId = IndexedId(row.Principal),
                Depth = row.Depth,
                Level = row.IsBlock ? 0 : (int)row.Level,
                IsBlock = row.IsBlock,
                IsDirect = row.IsDirect,
                StartsUtc = row.StartsUtc,
                EndsUtc = row.EndsUtc,
                DepthKey = DepthKey(document.Kind, document.AccessKey, row.Depth),
            }));

        context.For<ShareRoleShadowIndex>()
            .Map(document => document.Shadows.Select(shadow => new ShareRoleShadowIndex
            {
                Kind = document.Kind,
                ObjectId = document.ObjectId,
                Role = IndexedId(SharePrincipal.Role(shadow.Role) with { OrganizationId = shadow.OrganizationId }),
                DepthKey = DepthKey(document.Kind, document.AccessKey, shadow.Depth),
                Level = (int)shadow.Level,
                StartsUtc = shadow.StartsUtc,
                EndsUtc = shadow.EndsUtc,
            }));
    }
}

public sealed class ShareRecalculationIndexProvider : IndexProvider<ShareRecalculation>
{
    public override void Describe(DescribeContext<ShareRecalculation> context) =>
        context.For<ShareRecalculationIndex>()
            .Map(recalculation => new ShareRecalculationIndex
            {
                Kind = recalculation.Kind,
                ObjectId = recalculation.ObjectId,
                RequestedUtc = recalculation.RequestedUtc,
            });
}
