#nullable enable

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Grove.Api;
using Grove.Access;
using ISession = YesSql.ISession;
using Grove.Users;
using Grove.Users.Indexes;
using Grove.Users.Models;
using Grove.Users.Services;
using Grove.Security.Services;
using YesSql;

namespace Grove.Users.Api;

public sealed class UsersApi : ApiContributor
{
    public override string Name => "Users";

    public override string FeatureId => "Grove.Users";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("users");
        routes.MapGet("", (HttpContext __http, [FromQuery] string? search, [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 50) => ApiHandlers.For<UsersHandlers>(__http).ListAsync(search, status, page, pageSize));
        routes.MapGet("{id}", (HttpContext __http, [FromRoute] string id) => ApiHandlers.For<UsersHandlers>(__http).GetAsync(id));
        routes.MapPost("", (HttpContext __http, [FromBody] UserWrite request) => ApiHandlers.For<UsersHandlers>(__http).CreateAsync(request));
        routes.MapPut("{id}", (HttpContext __http, [FromRoute] string id, [FromBody] UserWrite request) => ApiHandlers.For<UsersHandlers>(__http).SaveAsync(id, request));
        routes.MapPost("{id}/enabled", (HttpContext __http, [FromRoute] string id, [FromBody] UserEnabled request) => ApiHandlers.For<UsersHandlers>(__http).SetEnabledAsync(id, request));
        routes.MapDelete("{id}", (HttpContext __http, [FromRoute] string id) => ApiHandlers.For<UsersHandlers>(__http).DeleteAsync(id));
        routes.MapGet("{id}/status", (HttpContext __http, [FromRoute] string id) => ApiHandlers.For<UsersHandlers>(__http).StatusAsync(id));
        routes.MapPost("{id}/unlock", (HttpContext __http, [FromRoute] string id) => ApiHandlers.For<UsersHandlers>(__http).UnlockAsync(id));
        routes.MapPost("{id}/confirm-email", (HttpContext __http, [FromRoute] string id) => ApiHandlers.For<UsersHandlers>(__http).ConfirmEmailAsync(id));
        routes.MapPost("{id}/send-verification", (HttpContext __http, [FromRoute] string id) => ApiHandlers.For<UsersHandlers>(__http).SendVerificationAsync(id));
        routes.MapGet("{id}/external-logins", (HttpContext __http, [FromRoute] string id) => ApiHandlers.For<UsersHandlers>(__http).ExternalLoginsAsync(id));
        routes.MapDelete("{id}/external-logins/{provider}/{key}", (HttpContext __http, [FromRoute] string id, [FromRoute] string provider, [FromRoute] string key) => ApiHandlers.For<UsersHandlers>(__http).RemoveExternalLoginAsync(id, provider, key));
    }
}

public sealed class UsersHandlers(UserManager<IUser> userManager, IUserService users, IAuthorizationService authorization, IPermissionInvalidator permissions, ISession session, IRoleService roles) : ApiHandlers
{
    public async Task<IResult> ListAsync([FromQuery] string? search, [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        if (!await authorization.AuthorizeAsync(User, UsersPermissions.ListUsers, new User())) return Forbid();

        // userManager.Users (IQueryableUserStore<TUser>) is unusable here - stock
        // Grove's UserStore never implements that interface, so the property
        // throws NotSupportedException regardless of tenant/data provider. Query the
        // YesSql UserIndex directly instead (same pattern as Grove.Users'
        // own AdminController), then materialize the matching User documents.
        var query = session.Query<User, UserIndex>();
        if (string.Equals(status, "enabled", StringComparison.OrdinalIgnoreCase)) query = query.Where(index => index.IsEnabled);
        if (string.Equals(status, "disabled", StringComparison.OrdinalIgnoreCase)) query = query.Where(index => !index.IsEnabled);

        var allUsers = await query.ListAsync();
        IEnumerable<User> matching = allUsers;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            matching = matching.Where(user => (user.UserName ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase) || (user.Email ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        // Scoped to the users the caller may list (docs/access.md, recorded defects): the base
        // ListUsers sees everyone; otherwise ListUsers is asked against each role (the per-role
        // ListUsersInRole_{role}), and a user is listed when one of their roles is, or - holding
        // none - when an assignable role is, as the role mapper decides for one user.
        IEnumerable<User> listable = matching;
        if (!await authorization.AuthorizeAsync(User, UsersPermissions.ListUsers))
        {
            var listableRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var role in await roles.GetRolesAsync())
            {
                if (await authorization.AuthorizeAsync(User, UsersPermissions.ListUsers, role))
                {
                    listableRoles.Add(role.RoleName);
                }
            }

            var roleless = (await roles.GetAssignableRolesAsync()).Any(role => listableRoles.Contains(role.RoleName));
            listable = matching.Where(user => user.RoleNames is { Count: > 0 } names ? names.Any(listableRoles.Contains) : roleless);
        }

        var ordered = listable.OrderBy(user => user.UserName, StringComparer.OrdinalIgnoreCase).ToArray();
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        return Ok(new UserList(ordered.Length, ordered.Skip((page - 1) * pageSize).Take(pageSize).Select(UserModel.From).ToArray()));
    }

    public async Task<IResult> GetAsync(string id)
    {
        var user = await FindAsync(id); if (user is null) return NotFound();
        if (!await authorization.AuthorizeAsync(User, UsersPermissions.ViewUsers, user)) return Forbid();
        return Ok(UserModel.From(user));
    }

    public async Task<IResult> CreateAsync([FromBody] UserWrite request)
    {
        var user = new User();
        if (!await authorization.AuthorizeAsync(User, UsersPermissions.EditUsers, user)) return Forbid();
        if (string.IsNullOrWhiteSpace(request.Password)) return BadRequest("A password is required.");
        Apply(request, user);
        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded) return BadRequest(result.Errors.Select(error => error.Description));
        await SetRolesAsync(user, request.Roles);
        await permissions.InvalidateTenantAsync(HttpContext.RequestAborted);
        return Created($"api/grove/users/{user.UserId}", UserModel.From(user));
    }

    public async Task<IResult> SaveAsync(string id, [FromBody] UserWrite request)
    {
        var user = await FindAsync(id); if (user is null) return NotFound();
        if (!await authorization.AuthorizeAsync(User, UsersPermissions.EditUsers, user)) return Forbid();
        Apply(request, user);
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded) return BadRequest(result.Errors.Select(error => error.Description));
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            result = await userManager.ResetPasswordAsync(user, token, request.Password);
            if (!result.Succeeded) return BadRequest(result.Errors.Select(error => error.Description));
        }
        await SetRolesAsync(user, request.Roles);
        await permissions.InvalidateTenantAsync(HttpContext.RequestAborted);
        return Ok(UserModel.From(user));
    }

    public async Task<IResult> SetEnabledAsync(string id, [FromBody] UserEnabled request)
    {
        var user = await FindAsync(id); if (user is null) return NotFound();
        if (!await authorization.AuthorizeAsync(User, UsersPermissions.EditUsers, user)) return Forbid();
        if (User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value == user.UserId && !request.Enabled) return BadRequest("You cannot disable your own account.");
        var changed = request.Enabled ? await users.EnableAsync(user) : await users.DisableAsync(user);
        if (changed) await permissions.InvalidateTenantAsync(HttpContext.RequestAborted);
        return changed ? Ok(UserModel.From(user)) : BadRequest("The user status could not be changed.");
    }

    public async Task<IResult> DeleteAsync(string id)
    {
        var user = await FindAsync(id); if (user is null) return NotFound();
        if (!await authorization.AuthorizeAsync(User, UsersPermissions.DeleteUsers, user)) return Forbid();
        if (User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value == user.UserId) return BadRequest("You cannot delete your own account.");
        var result = await userManager.DeleteAsync(user);
        if (result.Succeeded) await permissions.InvalidateTenantAsync(HttpContext.RequestAborted);
        return result.Succeeded ? NoContent() : BadRequest(result.Errors.Select(error => error.Description));
    }

    /// <summary>The account's state the editor shows beside its fields: lockout and failed sign-ins.</summary>
    public async Task<IResult> StatusAsync(string id)
    {
        var user = await FindAsync(id); if (user is null) return NotFound();
        if (!await authorization.AuthorizeAsync(User, UsersPermissions.ViewUsers, user)) return Forbid();
        var lockoutEnd = await userManager.GetLockoutEndDateAsync(user);
        return Ok(new UserStatus(lockoutEnd is { } end && end > DateTimeOffset.UtcNow, lockoutEnd, await userManager.GetAccessFailedCountAsync(user)));
    }

    /// <summary>Clears a lockout and the failed sign-in count.</summary>
    public async Task<IResult> UnlockAsync(string id)
    {
        var user = await FindAsync(id); if (user is null) return NotFound();
        if (!await authorization.AuthorizeAsync(User, UsersPermissions.EditUsers, user)) return Forbid();
        await userManager.ResetAccessFailedCountAsync(user);
        var result = await userManager.SetLockoutEndDateAsync(user, null);
        if (!result.Succeeded)
        {
            await session.CancelAsync();
            return BadRequest(result.Errors.Select(error => error.Description));
        }

        return Ok(new UserStatus(false, null, 0));
    }

    /// <summary>Confirms the user's email address on the administrator's word.</summary>
    public async Task<IResult> ConfirmEmailAsync(string id)
    {
        var user = await FindAsync(id); if (user is null) return NotFound();
        if (!await authorization.AuthorizeAsync(User, UsersPermissions.EditUsers, user)) return Forbid();
        if (!await userManager.IsEmailConfirmedAsync(user))
        {
            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
            var result = await userManager.ConfirmEmailAsync(user, token);
            if (!result.Succeeded) return BadRequest(result.Errors.Select(error => error.Description));
        }

        return Ok(UserModel.From(user));
    }

    /// <summary>Sends the user a new email verification message (needs Grove.Email).</summary>
    public async Task<IResult> SendVerificationAsync(string id)
    {
        var user = await FindAsync(id); if (user is null) return NotFound();
        if (!await authorization.AuthorizeAsync(User, UsersPermissions.EditUsers, user)) return Forbid();
        if (await userManager.IsEmailConfirmedAsync(user)) return BadRequest("The email address is already confirmed.");
        if (HttpContext.RequestServices.GetService(typeof(UserEmailService)) is not UserEmailService email) return BadRequest("Email is not enabled on this tenant.");
        return await email.SendEmailConfirmationAsync(user) ? NoContent() : BadRequest("The verification message could not be sent.");
    }

    /// <summary>The external sign-ins (Microsoft, Google, OpenID...) linked to the account.</summary>
    public async Task<IResult> ExternalLoginsAsync(string id)
    {
        var user = await FindAsync(id); if (user is null) return NotFound();
        if (!await authorization.AuthorizeAsync(User, UsersPermissions.ViewUsers, user)) return Forbid();
        return Ok((await userManager.GetLoginsAsync(user)).Select(login => new UserExternalLogin(login.LoginProvider, login.ProviderKey, login.ProviderDisplayName)).ToArray());
    }

    /// <summary>Unlinks one external sign-in from the account.</summary>
    public async Task<IResult> RemoveExternalLoginAsync(string id, string provider, string key)
    {
        var user = await FindAsync(id); if (user is null) return NotFound();
        if (!await authorization.AuthorizeAsync(User, UsersPermissions.EditUsers, user)) return Forbid();
        var result = await userManager.RemoveLoginAsync(user, provider, key);
        return result.Succeeded ? NoContent() : BadRequest(result.Errors.Select(error => error.Description));
    }

    private async Task<User?> FindAsync(string id) => await userManager.FindByIdAsync(id) as User;
    private static void Apply(UserWrite request, User user) { user.UserName = request.UserName?.Trim(); user.Email = request.Email?.Trim(); user.PhoneNumber = request.PhoneNumber?.Trim(); user.EmailConfirmed = request.EmailConfirmed; user.IsEnabled = request.IsEnabled; }
    private async Task SetRolesAsync(User user, string[]? roles)
    {
        var target = (roles ?? []).Where(role => !string.IsNullOrWhiteSpace(role)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var current = await userManager.GetRolesAsync(user);
        var remove = current.Except(target, StringComparer.OrdinalIgnoreCase).ToArray();
        var add = target.Except(current, StringComparer.OrdinalIgnoreCase).ToArray();
        if (remove.Length > 0) await userManager.RemoveFromRolesAsync(user, remove);
        if (add.Length > 0) await userManager.AddToRolesAsync(user, add);
    }
}
