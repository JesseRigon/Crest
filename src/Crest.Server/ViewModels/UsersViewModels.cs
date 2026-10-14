using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Crest.Services;
using Crest.Users;
using Crest.Users.Indexes;
using Crest.Users.Models;
using Crest.Users.Services;
using YesSql;

namespace Crest.ViewModels;

public sealed record UserList(int Total, CrestUser[] Items);

public sealed record CrestUser(string Id, string? UserName, string? Email, string? PhoneNumber, bool EmailConfirmed, bool IsEnabled, bool TwoFactorEnabled, string[] Roles)
{
    public static CrestUser From(User user) => new(user.UserId, user.UserName, user.Email, user.PhoneNumber, user.EmailConfirmed, user.IsEnabled, user.TwoFactorEnabled, user.RoleNames?.ToArray() ?? []);
}

public sealed record UserWrite(string? UserName, string? Email, string? PhoneNumber, bool EmailConfirmed, bool IsEnabled, string[]? Roles, string? Password);

public sealed record UserEnabled(bool Enabled);

public sealed record UserStatus(bool IsLockedOut, DateTimeOffset? LockoutEnd, int AccessFailedCount);

public sealed record UserExternalLogin(string LoginProvider, string ProviderKey, string? ProviderDisplayName);
