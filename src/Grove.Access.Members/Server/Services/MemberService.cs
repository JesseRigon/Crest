using Grove.Entities;
using Grove.Access.Members.Constants;
using Grove.Access.Members.Indexes;
using Grove.Access.Members.Models;
using Grove.Parties.Services;
using Grove.Access.Users;
using Grove.Access.Users.Models;
using Grove.Access.Users.Services;
using YesSql;

namespace Grove.Access.Members.Services;

public class MemberService(
    ISession session,
    IUserService userService,
    IPartyUserLinkService partyUserLink,
    MemberBindingService bindings,
    Grove.Access.ICallerContextAccessor callers,
    Grove.Access.IScopeSetProvider scopes) : IMemberService
{
    public async Task<MemberModel?> GetAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await session.Query<User, UserConnectionIndex>(index => index.UserId == userId && index.Class == UserClasses.Member)
            .FirstOrDefaultAsync(cancellationToken);

        return user is null ? null : ToModel(user);
    }

    public async Task<IReadOnlyList<MemberModel>> ListByOrganizationAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        // The caller's connection scope bounds the list (docs/access.md, Domains onto the
        // extension system): a caller acting in an organization reads its own organization's
        // members only; no caller reads nothing.
        var caller = callers.Current;
        if (caller is null)
        {
            return [];
        }

        var rule = (await scopes.GetAsync(caller, cancellationToken)).Require(nameof(UserConnectionIndex));
        if (rule.Kind == Grove.Access.ScopeKind.None)
        {
            return [];
        }

        var query = session.Query<User, UserConnectionIndex>(index => index.OrganizationId == organizationId && index.Class == UserClasses.Member);
        if (rule.Kind == Grove.Access.ScopeKind.Filter)
        {
            query = query.Where(Grove.Data.Scoping.ScopeExpressions.ToPredicate<UserConnectionIndex>(rule));
        }

        var users = await query.ListAsync(cancellationToken);

        // Another organization's bindings of the same user stay out of an organization's view.
        var models = users.Select(ToModel);
        if (MemberResourcePermissionMapper.ActsInOrganization(caller))
        {
            models = models.Select(model => model with { Bindings = model.Bindings.Where(binding => binding.OrganizationId == caller.OrganizationId).ToArray() });
        }

        return models.ToArray();
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
        await bindings.StampAsync(user, request.OrganizationId, UserClasses.Member, request.PersonId, request.Roles, cancellationToken);

        var created = await userService.CreateUserAsync(user, request.Password, reportError);
        if (created is null)
        {
            return null;
        }

        // The user has its id now; the node joins the same unit of work.
        await bindings.AddHierarchyNodeAsync(request.OrganizationId, user.UserId, cancellationToken);

        // The other half of the party <-> user link: the member records its person,
        // the person records its portal user. An unknown person id is a caller error.
        if (!string.IsNullOrWhiteSpace(request.PersonId)
            && !await partyUserLink.LinkPortalUserAsync(request.PersonId, user.UserId, cancellationToken))
        {
            throw new InvalidOperationException($"'{request.PersonId}' is not a Person content item.");
        }

        return created;
    }

    internal static MemberModel ToModel(User user)
    {
        user.TryGet<GroveMemberInfo>(out var memberInfo);
        return new MemberModel(
            user.UserId,
            user.UserName ?? string.Empty,
            user.Email,
            memberInfo?.PersonId,
            (memberInfo?.Bindings ?? [])
                .Where(binding => binding.Class == UserClasses.Member)
                .Select(binding => new MemberOrgBindingModel(binding.OrganizationId, binding.Roles, binding.IsMemberAdmin))
                .ToArray());
    }
}
