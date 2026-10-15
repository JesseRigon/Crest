using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Grove.Components.Access;

// The editors' view of the permission model API (api/grove/permission-catalog,
// permission-sets, roles/{name}, access/*; docs/permissions.md › Editors).

public sealed record PermissionInfo(string Name, string? Description, string? Category, bool IsSecurityCritical);

public sealed record PermissionSetInfo(string Name, string? Description, List<string> Permissions);

public sealed record RoleDetail(string Name, string? Description, List<string> Sets, List<string> Permissions, bool IsAdmin, bool IsSystem);

public sealed record RoleWrite(string Name, string? Description, List<string>? Sets, List<string>? Permissions);

public sealed record AccessReachInfo(string? OrganizationId);

public sealed record AccessGroupInfo(string Id, string Name, string? Description, AccessReachInfo Reach, List<string> Roles, List<string> UserIds, string? TemplateId);

public sealed record AccessGroupWrite(string Name, string? Description, List<string> Roles);

public sealed record GroupUserRequest(string UserId);

[JsonConverter(typeof(JsonStringEnumConverter<OverrideLevel>))]
public enum OverrideLevel { Role, Set, Permission }

[JsonConverter(typeof(JsonStringEnumConverter<OverrideEffect>))]
public enum OverrideEffect { Grant, Deny }

public sealed record UserAccessOverride(OverrideLevel Level, string Name, OverrideEffect Effect);

public sealed record UserOverridesModel(List<UserAccessOverride> Overrides);

public sealed record UserAccessGrant(string Permission, string Source);

public sealed record UserAccessInfo(string UserId, List<string> Groups, List<string> Roles, List<string> Sets, List<UserAccessGrant> Permissions);

public sealed class OrganizationAccessDefaults
{
    public List<string> DefaultGroupIds { get; set; } = [];

    public List<string> AssignableRoles { get; set; } = [];

    public List<string> OwnerLockedPermissions { get; set; } = [];
}

public sealed record UserSummary(string Id, string? UserName, string? Email);

public sealed record UserListResult(int Total, List<UserSummary> Items);

/// <summary>Reads an error body ({ error }) the access API answers a refusal with.</summary>
public static class AccessApi
{
    public static async Task<string> ErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
            if (body is not null && body.TryGetValue("error", out var error))
            {
                return error?.ToString() ?? response.ReasonPhrase ?? "The request failed.";
            }
        }
        catch
        {
        }

        return response.StatusCode == System.Net.HttpStatusCode.Forbidden ? "You may not change this." : response.ReasonPhrase ?? "The request failed.";
    }
}

/// <summary>A user a group editor may add: the id and how to show it.</summary>
public sealed record UserChoice(string Id, string Label);

/// <summary>A user the caller manages at a reach (api/grove/access/managed-users).</summary>
public sealed record ManagedUserInfo(string Id, string Label);

/// <summary>An organization's owner, and whether the caller may transfer it.</summary>
public sealed record OwnershipInfo(string OrganizationId, string? OwnerUserId, bool CanTransfer);

public sealed record TransferOwnershipRequest(string UserId);
