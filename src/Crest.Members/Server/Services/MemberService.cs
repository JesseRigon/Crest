using Crest.Members.Constants;
using Crest.Members.Indexes;
using Crest.Members.Models;
using Crest.Parties.Services;
using Crest.Entities;
using Crest.Users;
using Crest.Users.Models;
using Microsoft.Extensions.DependencyInjection;
using Crest.Environment.Shell.Scope;
using Crest.Users.Services;
using YesSql;

namespace Crest.Members.Services;

public class MemberService : IMemberService
{
    private readonly ISession _session;
    private readonly IUserService _userService;
    private readonly IUserHierarchyService _hierarchy;
    private readonly IPartyUserLinkService _partyUserLink;
    private readonly MemberStampService _stamps;
    private readonly Crest.Access.IPermissionVersion _permissionVersion;

    public MemberService(ISession session, IUserService userService, IUserHierarchyService hierarchy, IPartyUserLinkService partyUserLink, MemberStampService stamps, Crest.Access.IPermissionVersion permissionVersion)
    {
        _session = session;
        _permissionVersion = permissionVersion;
        _userService = userService;
        _hierarchy = hierarchy;
        _partyUserLink = partyUserLink;
        _stamps = stamps;
    }

    public async Task<MemberModel?> GetAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _session.Query<User, UserClassIndex>(index => index.UserId == userId && index.Class == UserClasses.Member)
            .FirstOrDefaultAsync(cancellationToken);

        return user is null ? null : ToModel(user);
    }

    public async Task<IReadOnlyList<MemberModel>> ListByOrganizationAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        var users = await _session.Query<User, MemberOrgBindingIndex>(index => index.OrganizationId == organizationId)
            .ListAsync(cancellationToken);

        return users.Select(ToModel).ToArray();
    }

    public async Task<IUser?> CreateMemberAsync(CreateMemberRequest request, Action<string, string> reportError, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(request.UserName);
        ArgumentException.ThrowIfNullOrEmpty(request.OrganizationId);

        var user = new User
        {
            UserName = request.UserName,
            Email = request.Email,
            EmailConfirmed = true,
            IsEnabled = true,
        };

        // Stamped BEFORE creation so the creating-handler leaves it alone.
        await _stamps.StampMemberAsync(user, request.OrganizationId, request.PersonId, request.Roles, cancellationToken);

        var created = await _userService.CreateUserAsync(user, request.Password, reportError);
        if (created is null)
        {
            return null;
        }

        // The other half of the party <-> user link: the member records its person,
        // the person records its portal user. An unknown person id is a caller error.
        if (!string.IsNullOrWhiteSpace(request.PersonId)
            && !await _partyUserLink.LinkPortalUserAsync(request.PersonId, user.UserId, cancellationToken))
        {
            throw new InvalidOperationException($"'{request.PersonId}' is not a Person content item.");
        }

        return created;
    }

    public async Task<MemberOrgBindingModel> AddBindingAsync(string userId, string organizationId, IReadOnlyList<string>? roles, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(organizationId);

        var user = await RequireMemberUserAsync(userId, cancellationToken);
        var memberInfo = user.GetOrCreate<CrestMemberInfo>();

        if (memberInfo.Bindings.Any(binding => binding.OrganizationId == organizationId))
        {
            throw new InvalidOperationException("The member is already bound to this organization.");
        }

        var binding = new MemberOrgBinding
        {
            OrganizationId = organizationId,
            Roles = (roles ?? [MemberRoleTemplates.Member]).ToList(),
            IsMemberAdmin = await _stamps.IsFirstMemberOfOrganizationAsync(organizationId, cancellationToken),
        };
        memberInfo.Bindings.Add(binding);
        user.Put(memberInfo);

        await _session.SaveAsync(user);
        // A binding is a rights write: every cached caller of this tenant is stale now.
        await _permissionVersion.BumpAsync(cancellationToken);
        MemberStampService.DeferAddHierarchyNode(organizationId, user);

        // Same seam as the creation path: the binding is reported once it has committed,
        // flagged when this member is the organization's first admin.
        MemberStampService.DeferMemberBound(organizationId, user, binding.IsMemberAdmin);

        return new MemberOrgBindingModel(binding.OrganizationId, binding.Roles, binding.IsMemberAdmin);
    }

    public async Task RemoveBindingAsync(string userId, string organizationId, CancellationToken cancellationToken = default)
    {
        var user = await RequireMemberUserAsync(userId, cancellationToken);
        var memberInfo = user.GetOrCreate<CrestMemberInfo>();

        var removed = memberInfo.Bindings.RemoveAll(binding => binding.OrganizationId == organizationId);
        if (removed == 0)
        {
            throw new InvalidOperationException("The member is not bound to this organization.");
        }

        user.Put(memberInfo);
        await _session.SaveAsync(user);
        await _permissionVersion.BumpAsync(cancellationToken);

        DeferRemoveHierarchyNodes(organizationId, user.UserId);
        DeferMemberUnbound(organizationId, user.UserId);
    }

    /// <summary>
    /// Leaving an organization ends the (user, organization) relationship, so everything
    /// keyed to that pair goes with it — which is a matter for whoever keeps such records
    /// (<see cref="IMemberLifecycleHandler"/>), not for Members. Other organizations are
    /// untouched: each is a different composite key.
    /// </summary>
    /// <remarks>
    /// Deferred for the same reason the hierarchy removal is: this runs inside the
    /// ambient session's write, and a handler goes through the content manager, which
    /// must not re-enter an uncommitted session.
    /// </remarks>
    private static void DeferMemberUnbound(string organizationId, string userId)
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var context = new MemberUnboundContext(userId, organizationId);
            foreach (var handler in scope.ServiceProvider.GetServices<IMemberLifecycleHandler>())
            {
                await handler.MemberUnboundAsync(context);
            }
        });
    }

    private static void DeferRemoveHierarchyNodes(string organizationId, string userId)
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var hierarchy = scope.ServiceProvider.GetRequiredService<IUserHierarchyService>();
            var root = HierarchyRoot.ForOrganization(organizationId);
            foreach (var node in (await hierarchy.GetNodesForUserAsync(userId))
                .Where(node => node.RootKind == HierarchyRootKinds.Organization && node.OrganizationId == organizationId))
            {
                await hierarchy.RemoveAsync(root, node.Id);
            }
        });
    }

    private async Task<User> RequireMemberUserAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await _session.Query<User, UserClassIndex>(index => index.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Unknown user.");

        if (!user.TryGet<CrestUserClass>(out var userClass) || userClass.Class != UserClasses.Member)
        {
            throw new InvalidOperationException("The user is not a member-class account.");
        }

        return user;
    }

    private static MemberModel ToModel(User user)
    {
        user.TryGet<CrestMemberInfo>(out var memberInfo);
        return new MemberModel(
            user.UserId,
            user.UserName ?? string.Empty,
            user.Email,
            memberInfo?.PersonId,
            (memberInfo?.Bindings ?? [])
                .Select(binding => new MemberOrgBindingModel(binding.OrganizationId, binding.Roles, binding.IsMemberAdmin))
                .ToArray());
    }
}
