using Grove.Access;
using Grove.Email;
using Grove.Mvc.Utilities;
using Grove.Security;
using Grove.Users.Models;
using Grove.Users.Services;
using Grove.Users;
using Grove.Workflows.Activities.Flowchart.Attributes;
using Grove.Workflows.Attributes;
using Grove.Workflows.Extensions;
using Grove.Workflows.Models;
using Grove.Workflows.UIHints;
using Grove.Workflows;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Grove.Users.Workflows;

/// <summary>
/// Creates a user from an e-mail address and a user name (defaulting to the e-mail with the
/// @ replaced), optionally held for moderation, and optionally sends the confirmation e-mail
/// with a link the flow may also read from the ConfirmationUrl output.
/// </summary>
[Activity("Grove.Users", "User", "Creates a user and optionally sends the e-mail confirmation.", DisplayName = "Create user")]
[FlowNode("Done", "Failed")]
public class CreateUser : Activity
{

    [Input(DisplayName = "Email", UIHint = InputUIHints.SingleLine)]
    public Input<string> Email { get; set; } = null!;

    [Input(DisplayName = "User name", Description = "Empty = the e-mail address with '@' replaced by '+'.", UIHint = InputUIHints.SingleLine)]
    public Input<string?> UserName { get; set; } = null!;

    [Input(DisplayName = "Require moderation", Description = "Create the user disabled, for an administrator to enable.")]
    public Input<bool> RequireModeration { get; set; } = new(false);

    [Input(DisplayName = "Send confirmation email", Description = "Send the e-mail confirmation link to the new user.")]
    public Input<bool> SendConfirmationEmail { get; set; } = new(true);

    [Input(DisplayName = "Confirmation email subject", UIHint = InputUIHints.SingleLine)]
    public Input<string?> ConfirmationEmailSubject { get; set; } = null!;

    [Input(DisplayName = "Confirmation email body", Description = "HTML. The confirmation link is available to expressions as the ConfirmationUrl output.", UIHint = InputUIHints.MultiLine)]
    public Input<string?> ConfirmationEmailBody { get; set; } = null!;

    [Output(Description = "The new user's id.")]
    public Output<string?> UserId { get; set; } = null!;

    [Output(Description = "The e-mail confirmation link, when one was generated.")]
    public Output<string?> ConfirmationUrl { get; set; } = null!;

    [Output(Description = "Why the activity ended on Failed, when it did.")]
    public Output<string?> Failure { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var email = Email.GetOrDefault(context)?.Trim();
        if (string.IsNullOrEmpty(email))
        {
            await FailAsync(context, "No e-mail address.");
            return;
        }

        var userName = UserName.GetOrDefault(context)?.Trim();
        if (string.IsNullOrEmpty(userName))
        {
            userName = email.Replace('@', '+');
        }

        // The burst's caller must be allowed to create users: a flow started by an anonymous
        // visitor creates one only when its definition is published as system.
        await context.GetRequiredService<IAccessDecision>().RequireAsync(context.GetRequiredService<ICallerContextAccessor>().Current, UsersPermissions.EditUsers.Name, cancellationToken: context.CancellationToken);

        var errors = new List<string>();
        var created = await context.GetRequiredService<IUserService>().CreateUserAsync(new User
        {
            UserName = userName,
            Email = email,
            IsEnabled = !RequireModeration.GetOrDefault(context),
        }, null, (key, message) => errors.Add($"{key}: {message}"));

        if (created is not User user || errors.Count > 0)
        {
            await FailAsync(context, errors.Count > 0 ? string.Join("; ", errors) : "The user was not created.");
            return;
        }

        UserId.Set(context, user.UserId);

        if (SendConfirmationEmail.GetOrDefault(context))
        {
            var httpContext = context.GetService<IHttpContextAccessor>()?.HttpContext;
            if (httpContext is null)
            {
                await FailAsync(context, "The confirmation e-mail needs a request to build its link; run this activity from a request-started flow or turn the confirmation off.");
                return;
            }

            var code = await context.GetRequiredService<UserManager<IUser>>().GenerateEmailConfirmationTokenAsync(user);
            var url = Services.AccountPages.ConfirmEmailUrl(httpContext.Request, user.UserId, code);
            ConfirmationUrl.Set(context, url);

            var body = (ConfirmationEmailBody.GetOrDefault(context) ?? string.Empty).Replace("{{ConfirmationUrl}}", url, StringComparison.OrdinalIgnoreCase);
            var result = await context.GetRequiredService<IEmailService>().SendAsync(email, ConfirmationEmailSubject.GetOrDefault(context) ?? string.Empty, body);
            if (!result.Succeeded)
            {
                await FailAsync(context, string.Join("; ", result.Errors.Select(error => error.Message?.ToString())));
                return;
            }
        }

        await context.CompleteActivityWithOutcomesAsync("Done");
    }

    private async ValueTask FailAsync(ActivityExecutionContext context, string reason)
    {
        Failure.Set(context, reason);
        context.GetRequiredService<ILogger<CreateUser>>().LogWarning("Create user failed: {Reason}", reason);
        await context.CompleteActivityWithOutcomesAsync("Failed");
    }
}

/// <summary>
/// Branches on the run's caller: Anonymous, Authenticated, or InRole when they hold one of
/// the given roles. The caller is the one the access gate built for the burst.
/// </summary>
[Activity("Grove.Users", "User", "Branches on whether the run's caller is anonymous, authenticated or in one of the given roles.", DisplayName = "Validate user")]
[FlowNode("Anonymous", "Authenticated", "InRole")]
public class ValidateUser : Activity
{
    [Input(DisplayName = "Roles", Description = "Role names; the InRole outcome fires when the user holds any of them.", UIHint = InputUIHints.MultiText)]
    public Input<ICollection<string>> Roles { get; set; } = null!;

    [Output(Description = "The authenticated user's name.")]
    public Output<string?> UserName { get; set; } = null!;

    [Output(Description = "The authenticated user's role names.")]
    public Output<ICollection<string>> UserRoles { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        // The burst's caller, as the gate built it: its roles are server-held, the member
        // binding's included, and it is there in a background burst too.
        var caller = context.GetService<ICallerContextAccessor>()?.Current;
        if (caller is not { IsAuthenticated: true })
        {
            await context.CompleteActivityWithOutcomesAsync("Anonymous");
            return;
        }

        UserName.Set(context, caller.UserName);

        var userRoles = caller.Roles.ToList();
        UserRoles.Set(context, userRoles);

        var roles = Roles.GetOrDefault(context) ?? [];
        if (roles.Any(role => userRoles.Contains(role, StringComparer.OrdinalIgnoreCase)))
        {
            await context.CompleteActivityWithOutcomesAsync("InRole");
            return;
        }

        await context.CompleteActivityWithOutcomesAsync("Authenticated");
    }
}

/// <summary>Adds a user to a role.</summary>
[Activity("Grove.Users", "User", "Adds a user to a role.", DisplayName = "Assign user role")]
[FlowNode("Done", "Failed")]
public class AssignUserRole : Activity
{
    [Input(DisplayName = "User name", Description = "The user's name or e-mail.", UIHint = InputUIHints.SingleLine)]
    public Input<string> UserName { get; set; } = null!;

    [Input(DisplayName = "Role name", UIHint = InputUIHints.SingleLine)]
    public Input<string> RoleName { get; set; } = null!;

    [Output(Description = "Why the activity ended on Failed, when it did.")]
    public Output<string?> Failure { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var userName = UserName.GetOrDefault(context)?.Trim();
        var roleName = RoleName.GetOrDefault(context)?.Trim();
        if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(roleName))
        {
            await FailAsync(context, "User name and role name are required.");
            return;
        }

        if (await context.GetRequiredService<IUserService>().GetUserAsync(userName) is not User user)
        {
            await FailAsync(context, $"No user '{userName}'.");
            return;
        }

        // Assigning takes the right for that role (AssignRoleToUsers_{role}, or the base one).
        if (await context.GetRequiredService<RoleManager<IRole>>().FindByNameAsync(roleName) is not { } role)
        {
            await FailAsync(context, $"No role '{roleName}'.");
            return;
        }

        await context.GetRequiredService<IAccessDecision>().RequireAsync(context.GetRequiredService<ICallerContextAccessor>().Current, UsersPermissions.AssignRoleToUsers.Name, role, context.CancellationToken);

        if (!user.RoleNames.Contains(roleName, StringComparer.OrdinalIgnoreCase))
        {
            await context.GetRequiredService<UserManager<IUser>>().AddToRoleAsync(user, roleName);
        }

        await context.CompleteActivityWithOutcomesAsync("Done");
    }

    private async ValueTask FailAsync(ActivityExecutionContext context, string reason)
    {
        Failure.Set(context, reason);
        context.GetRequiredService<ILogger<AssignUserRole>>().LogWarning("Assign user role failed: {Reason}", reason);
        await context.CompleteActivityWithOutcomesAsync("Failed");
    }
}

/// <summary>Removes a user from roles; their security stamp is refreshed so open sessions re-evaluate.</summary>
[Activity("Grove.Users", "User", "Removes a user from roles and invalidates their sessions.", DisplayName = "Unassign user role")]
[FlowNode("Done", "Failed")]
public class UnassignUserRole : Activity
{
    [Input(DisplayName = "User name", Description = "The user's name or e-mail.", UIHint = InputUIHints.SingleLine)]
    public Input<string> UserName { get; set; } = null!;

    [Input(DisplayName = "Roles", Description = "The roles to remove.", UIHint = InputUIHints.MultiText)]
    public Input<ICollection<string>> Roles { get; set; } = null!;

    [Output(Description = "Why the activity ended on Failed, when it did.")]
    public Output<string?> Failure { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var userName = UserName.GetOrDefault(context)?.Trim();
        if (string.IsNullOrEmpty(userName))
        {
            await FailAsync(context, "No user name.");
            return;
        }

        if (await context.GetRequiredService<IUserService>().GetUserAsync(userName) is not User user)
        {
            await FailAsync(context, $"No user '{userName}'.");
            return;
        }

        var userManager = context.GetRequiredService<UserManager<IUser>>();
        var roleManager = context.GetRequiredService<RoleManager<IRole>>();
        var decision = context.GetRequiredService<IAccessDecision>();
        var removed = false;
        foreach (var role in Roles.GetOrDefault(context) ?? [])
        {
            if (user.RoleNames.Contains(role, StringComparer.OrdinalIgnoreCase))
            {
                await decision.RequireAsync(context.GetRequiredService<ICallerContextAccessor>().Current, UsersPermissions.AssignRoleToUsers.Name, await roleManager.FindByNameAsync(role) ?? (object)role, context.CancellationToken);
                await userManager.RemoveFromRoleAsync(user, role);
                removed = true;
            }
        }

        if (removed)
        {
            // Open sessions re-evaluate on the next request.
            await userManager.UpdateSecurityStampAsync(user);
        }

        await context.CompleteActivityWithOutcomesAsync("Done");
    }

    private async ValueTask FailAsync(ActivityExecutionContext context, string reason)
    {
        Failure.Set(context, reason);
        context.GetRequiredService<ILogger<UnassignUserRole>>().LogWarning("Unassign user role failed: {Reason}", reason);
        await context.CompleteActivityWithOutcomesAsync("Failed");
    }
}

/// <summary>Lists the users in the given roles, as user id → user name.</summary>
[Activity("Grove.Users", "User", "Lists the users in the given roles.", DisplayName = "Get users by role")]
[FlowNode("Done")]
public class GetUsersByRole : Activity
{
    [Input(DisplayName = "Roles", Description = "The roles whose members to list.", UIHint = InputUIHints.MultiText)]
    public Input<ICollection<string>> Roles { get; set; } = null!;

    [Output(Description = "The users found: user id → user name.")]
    public Output<IDictionary<string, string>> Users { get; set; } = null!;

    [Output(Description = "The user ids found.")]
    public Output<ICollection<string>> UserIds { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var userManager = context.GetRequiredService<UserManager<IUser>>();
        var roleManager = context.GetRequiredService<RoleManager<IRole>>();
        var decision = context.GetRequiredService<IAccessDecision>();
        var caller = context.GetRequiredService<ICallerContextAccessor>().Current;
        var users = new Dictionary<string, string>();
        foreach (var role in Roles.GetOrDefault(context) ?? [])
        {
            // Listing a role's members takes ListUsers for that role; a role the caller may not
            // list contributes nothing.
            if (await roleManager.FindByNameAsync(role) is not { } found || !await decision.AllowsAsync(caller, UsersPermissions.ListUsers.Name, found, context.CancellationToken))
            {
                continue;
            }

            foreach (var member in await userManager.GetUsersInRoleAsync(role))
            {
                if (member is User user)
                {
                    users.TryAdd(user.UserId, user.UserName);
                }
            }
        }

        Users.Set(context, users);
        UserIds.Set(context, users.Keys.ToList());
        await context.CompleteActivityWithOutcomesAsync("Done");
    }
}
