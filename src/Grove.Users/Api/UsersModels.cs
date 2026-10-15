#nullable enable

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Grove.Users;
using Grove.Users.Indexes;
using Grove.Users.Models;
using Grove.Users.Services;
using YesSql;

namespace Grove.Users.Api;

public sealed record UserList(int Total, UserModel[] Items);

public sealed record UserModel(string Id, string? UserName, string? Email, string? PhoneNumber, bool EmailConfirmed, bool IsEnabled, bool TwoFactorEnabled, string[] Roles)
{
    public static UserModel From(User user) => new(user.UserId, user.UserName, user.Email, user.PhoneNumber, user.EmailConfirmed, user.IsEnabled, user.TwoFactorEnabled, user.RoleNames?.ToArray() ?? []);
}

public sealed record UserWrite(string? UserName, string? Email, string? PhoneNumber, bool EmailConfirmed, bool IsEnabled, string[]? Roles, string? Password);

public sealed record UserEnabled(bool Enabled);

public sealed record UserStatus(bool IsLockedOut, DateTimeOffset? LockoutEnd, int AccessFailedCount, bool TwoFactorEnabled = false);

public sealed record UserExternalLogin(string LoginProvider, string ProviderKey, string? ProviderDisplayName);
