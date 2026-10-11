using Crest.Access;

namespace Crest.Sharing.Services;

/// <summary>
/// Sharing's one registration with the access decision (docs/sharing.md › Ruling): for a
/// resource a registered shareable kind recognises, and a permission that kind maps to an
/// access level, the caller's resolved level decides - <see cref="PermissionCandidate.Granted"/>
/// or a denial - and no other mapper is consulted. An inactive object is denied here, not by a
/// ceiling: a ceiling would also deny tenant administrators, who pass at the decision's
/// super-user step before any mapper. A permission the kind does not map is left to the
/// module's own mapper. Being authoritative, it answers before every module mapper; a module
/// that ships a higher-priority authority claiming some of the same resources replaces it for
/// exactly those, and additive mappers can still add grants (docs/access.md › Mapper roles).
/// </summary>
public sealed class SharingPermissionMapper(IEnumerable<IShareableKind> registered, ShareableKinds kinds, ISharingResolver resolver) : IAuthoritativeResourcePermissionMapper
{
    public async ValueTask<IReadOnlyList<PermissionCandidate>?> MapAsync(string permission, object resource, CallerContext caller, CancellationToken cancellationToken = default)
    {
        if (!kinds.TryIdentify(registered, resource, out var target, out var kind))
        {
            return null;
        }

        // A kind whose name two features claim at one priority: denied until resolved.
        if (kind is null)
        {
            return [];
        }

        if (kind.LevelFor(permission) is not { } level)
        {
            return null;
        }

        var resolution = await resolver.ResolveAsync(caller, target, cancellationToken);
        return resolution.Allows(level) ? [PermissionCandidate.Granted] : [];
    }
}
