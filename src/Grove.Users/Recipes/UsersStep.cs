using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Identity;
using Grove.Recipes.Models;
using Grove.Recipes.Services;
using Grove.Users.Models;
using YesSql;

namespace Grove.Users.Recipes;

public sealed class UsersStep : NamedRecipeStepHandler
{
    private readonly UserManager<IUser> _userManager;
    private readonly ISession _session;
    private readonly Grove.Access.IPermissionVersion _permissionVersion;
    private readonly Grove.Security.Services.IRoleService _roleService;

    public UsersStep(
        UserManager<IUser> userManager,
        ISession session,
        Grove.Security.Services.IRoleService roleService,
        Grove.Access.IPermissionVersion permissionVersion = null)
        : base("Users")
    {
        _userManager = userManager;
        _session = session;
        _roleService = roleService;
        _permissionVersion = permissionVersion;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<UsersStepModel>();

        foreach (var importedUser in model.Users)
        {
            if (string.IsNullOrWhiteSpace(importedUser.UserName))
            {
                continue;
            }

            if (Grove.Access.SystemAccounts.IsSystemAccount(importedUser.UserId))
            {
                throw new InvalidOperationException($"'{importedUser.UserId}' is reserved for system accounts.");
            }

            var iUser = await _userManager.FindByIdAsync(importedUser.UserId);

            if (iUser is not User user)
            {
                user = new User
                {
                    UserId = importedUser.UserId,
                };
            }

            user.Email = importedUser.Email;
            user.UserName = importedUser.UserName;
            user.EmailConfirmed = importedUser.EmailConfirmed;
            user.PasswordHash = importedUser.PasswordHash;
            user.IsEnabled = importedUser.IsEnabled;
            user.NormalizedEmail = importedUser.NormalizedEmail;
            user.NormalizedUserName = importedUser.NormalizedUserName;
            user.SecurityStamp = importedUser.SecurityStamp;
            user.ResetToken = importedUser.ResetToken;
            user.AccessFailedCount = importedUser.AccessFailedCount;
            user.IsLockoutEnabled = importedUser.IsLockoutEnabled;
            user.TwoFactorEnabled = importedUser.TwoFactorEnabled;
            user.PhoneNumber = importedUser.PhoneNumber;
            user.PhoneNumberConfirmed = importedUser.PhoneNumberConfirmed;
            // Derived (business) roles are computed, never assigned (docs/sharing.md
            // requirement 45): a recipe naming one is refused.
            foreach (var roleName in importedUser.RoleNames ?? [])
            {
                if (await _roleService.IsDerivedRoleAsync(roleName))
                {
                    throw new Grove.Roles.DerivedRoleAssignmentException(roleName);
                }
            }

            user.RoleNames = importedUser.RoleNames;

            await _session.SaveAsync(user);
        }

        // Imported roles and enabled flags are rights writes made past the user store.
        if (_permissionVersion is not null && model.Users.Length > 0)
        {
            await _permissionVersion.BumpAsync();
        }
    }
}
