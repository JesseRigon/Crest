using Crest.Users.Models;
using Crest.Workflows;

namespace Crest.Users.Workflows;

/// <summary>The triggers the Users module raises.</summary>
public static class UserWorkflowTriggers
{
    public const string Created = "user.created";
    public const string Updated = "user.updated";
    public const string Deleted = "user.deleted";
    public const string Enabled = "user.enabled";
    public const string Disabled = "user.disabled";
    public const string Confirmed = "user.confirmed";
    public const string LoggedIn = "user.logged-in";
    public const string LoggedOut = "user.logged-out";

    /// <summary>
    /// The payload every user trigger carries: ids and scalars only (the activities re-read
    /// the user through the user service). Roles is the comma-separated role names.
    /// </summary>
    public static Dictionary<string, object> Payload(User user, string? provider = null)
    {
        var payload = new Dictionary<string, object>
        {
            ["UserId"] = user.UserId,
            ["UserName"] = user.UserName,
            ["Email"] = user.Email ?? string.Empty,
            ["Roles"] = string.Join(",", user.RoleNames),
        };

        if (provider is not null)
        {
            payload["Provider"] = provider;
        }

        return payload;
    }
}

/// <summary>What Users registers with the workflow registry: its triggers and activities.</summary>
public sealed class UsersWorkflowProvider : IWorkflowTriggerProvider, IWorkflowActivityProvider
{
    public IEnumerable<WorkflowTriggerDescriptor> Triggers =>
    [
        new(UserWorkflowTriggers.Created, "User created", WorkflowsConstants.Objects.User, "A user was created: payload UserId, UserName, Email, Roles.", 10),
        new(UserWorkflowTriggers.Updated, "User updated", WorkflowsConstants.Objects.User, "A user was updated: payload UserId, UserName, Email, Roles.", 20),
        new(UserWorkflowTriggers.Deleted, "User deleted", WorkflowsConstants.Objects.User, "A user was deleted: payload UserId, UserName, Email, Roles.", 30),
        new(UserWorkflowTriggers.Enabled, "User enabled", WorkflowsConstants.Objects.User, "A user was enabled.", 40),
        new(UserWorkflowTriggers.Disabled, "User disabled", WorkflowsConstants.Objects.User, "A user was disabled.", 50),
        new(UserWorkflowTriggers.Confirmed, "User confirmed", WorkflowsConstants.Objects.User, "A user confirmed their e-mail address.", 60),
        new(UserWorkflowTriggers.LoggedIn, "User logged in", WorkflowsConstants.Objects.User, "A user signed in: payload UserId, UserName, Email, Roles, Provider (empty for the local login).", 70),
        new(UserWorkflowTriggers.LoggedOut, "User logged out", WorkflowsConstants.Objects.User, "A user signed out.", 80),
    ];

    public IEnumerable<WorkflowActivityDescriptor> Activities =>
    [
        new("users.create", "Create user", WorkflowsConstants.Objects.User, "Crest.Users.CreateUser", "Creates a user from an e-mail address and optionally sends the confirmation e-mail.", 10, Category: "User"),
        new("users.validate", "Validate user", WorkflowsConstants.Objects.User, "Crest.Users.ValidateUser", "Branches on whether the request's user is anonymous, authenticated or in one of the given roles.", 20, Category: "User"),
        new("users.assign-role", "Assign user role", WorkflowsConstants.Objects.User, "Crest.Users.AssignUserRole", "Adds a user to a role.", 30, Category: "User"),
        new("users.unassign-role", "Unassign user role", WorkflowsConstants.Objects.User, "Crest.Users.UnassignUserRole", "Removes a user from roles and invalidates their sessions.", 40, Category: "User"),
        new("users.by-role", "Get users by role", WorkflowsConstants.Objects.User, "Crest.Users.GetUsersByRole", "Lists the users in the given roles.", 50, Category: "User"),
    ];
}
