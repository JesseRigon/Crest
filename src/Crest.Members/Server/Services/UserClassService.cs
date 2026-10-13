using Crest.Entities;
using Crest.Members.Constants;
using Crest.Members.Indexes;
using Crest.Members.Models;
using Crest.Users;
using Crest.Users.Indexes;
using Crest.Users.Models;
using Microsoft.AspNetCore.Identity;
using YesSql;

namespace Crest.Members.Services;

/// <summary>
/// Conversion as adding or removing a class (ruling 2026-10-05, both directions), the one
/// permission-gated action (ConvertUserClass) that replaced convert-to-staff. Adding staff
/// binds the user to the tenant organization and keeps their member bindings; adding
/// member binds them to an organization; removing a class ends every binding of it. The
/// security stamp is bumped either way, since the class decides which login surfaces
/// accept the account.
/// </summary>
public sealed class UserClassService(ISession session, MemberBindingService bindings, UserManager<IUser> userManager)
{
    public async Task<UserConnectionsModel?> GetAsync(string userId, CancellationToken cancellationToken = default) =>
        await FindAsync(userId, cancellationToken) is { } user ? ToModel(user) : null;

    public async Task<UserConnectionsModel> AddAsync(string userId, AddUserClassRequest request, CancellationToken cancellationToken = default)
    {
        var user = await FindAsync(userId, cancellationToken) ?? throw new InvalidOperationException("Unknown user.");
        var organizationId = request.Class switch
        {
            UserClasses.Staff => await bindings.TenantOrganizationIdAsync(cancellationToken),
            UserClasses.Member when !string.IsNullOrWhiteSpace(request.OrganizationId) => request.OrganizationId,
            UserClasses.Member => throw new InvalidOperationException("A member class needs an organization."),
            _ => throw new InvalidOperationException($"'{request.Class}' is not a user class."),
        };

        await bindings.AddAsync(user, organizationId, request.Class, request.Roles, cancellationToken);
        await userManager.UpdateSecurityStampAsync(user);
        return ToModel(user);
    }

    public async Task<UserConnectionsModel> RemoveAsync(string userId, string userClass, CancellationToken cancellationToken = default)
    {
        var user = await FindAsync(userId, cancellationToken) ?? throw new InvalidOperationException("Unknown user.");
        var ended = UserClassSet.Bindings(user, userClass).Select(binding => binding.OrganizationId).ToArray();
        if (ended.Length == 0)
        {
            throw new InvalidOperationException($"The user is not {userClass}.");
        }

        if (user.TryGet<CrestMemberInfo>(out var info) && info.Bindings.All(binding => binding.Class == userClass))
        {
            throw new InvalidOperationException("Removing the user's only class would leave them outside every organization.");
        }

        foreach (var organizationId in ended)
        {
            await bindings.RemoveAsync(user, organizationId, userClass, cancellationToken);
        }

        await userManager.UpdateSecurityStampAsync(user);
        return ToModel(user);
    }

    private Task<User?> FindAsync(string userId, CancellationToken cancellationToken) =>
        session.Query<User, UserIndex>(index => index.UserId == userId).FirstOrDefaultAsync(cancellationToken);

    private static UserConnectionsModel ToModel(User user)
    {
        var connections = user.TryGet<CrestMemberInfo>(out var info) ? info.Bindings : [];
        return new UserConnectionsModel(
            user.UserId,
            UserClassSet.Classes(user),
            [.. connections.Select(binding => new UserConnectionModel(binding.OrganizationId, binding.Class, binding.Roles, binding.IsMemberAdmin))]);
    }
}
