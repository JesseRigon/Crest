using System.Security.Claims;
using System.Text.Json;
using Grove.Workflows.Registry;
using Grove.Workflows.Security;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Grove.Security;
using Grove.Infrastructure.Settings;
using Xunit;

namespace Grove.Workflows.Tests;

// Phase 5 (docs/workflows.md): ownership properties round-trip through the stored JSON
// shape, the tier rules, and the access handler's place in the authorization pipeline.
public class WorkflowOwnershipAndAccessTests
{
    private static IDictionary<string, object> Stored(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, object>>(json)!;

    [Fact]
    public void Ownership_reads_the_stored_json_shape_and_defaults_to_tenant()
    {
        var info = WorkflowOwnershipInfo.From(Stored("""{"Grove.Ownership":"shipped","Grove.FlowKey":"orders.quote-to-order","Grove.FlowVersion":3,"Grove.Forked":true}"""));

        Assert.True(info.IsShipped);
        Assert.Equal("orders.quote-to-order", info.FlowKey);
        Assert.Equal(3, info.FlowVersion);
        Assert.True(info.Forked);

        Assert.True(WorkflowOwnershipInfo.From(Stored("""{"other":1}""")).IsTenant);
        Assert.True(WorkflowOwnershipInfo.From(null).IsTenant);
        Assert.True(WorkflowOwnershipInfo.From(Stored("""{"Grove.Ownership":"SYSTEM"}""")).IsSystem);
    }

    [Fact]
    public void Stamp_replaces_whatever_the_client_sent()
    {
        var properties = Stored("""{"Grove.Ownership":"tenant","Grove.Forked":false,"designer":{"x":1}}""");
        new WorkflowOwnershipInfo(WorkflowOwnership.Shipped, "crm.lead-to-customer", 2, Forked: true).Stamp(properties);

        var again = WorkflowOwnershipInfo.From(properties);
        Assert.True(again.IsShipped);
        Assert.Equal("crm.lead-to-customer", again.FlowKey);
        Assert.Equal(2, again.FlowVersion);
        Assert.True(again.Forked);
        Assert.True(properties.ContainsKey("designer"));

        WorkflowOwnershipInfo.TenantOwned.Stamp(properties);
        Assert.False(properties.ContainsKey(WorkflowsConstants.OwnershipProperty));
        Assert.False(properties.ContainsKey(WorkflowsConstants.FlowKeyProperty));
    }

    [Fact]
    public void Access_lists_read_arrays_strings_and_in_memory_collections()
    {
        var fromJson = WorkflowDefinitionAccessResource.From("d", Stored("""{"Grove.Access.Edit":["Editor"," alice "],"Grove.Access.Run":"Operators, bob"}"""));
        Assert.Equal(["Editor", "alice"], fromJson.Edit);
        Assert.Equal(["Operators", "bob"], fromJson.Run);

        var properties = new Dictionary<string, object>();
        WorkflowDefinitionAccessResource.Stamp(properties, new WorkflowDefinitionAccessModel(["Editor", "editor", ""], []));
        var inMemory = WorkflowDefinitionAccessResource.From("d", properties);
        Assert.Equal(["Editor"], inMemory.Edit);
        Assert.Empty(inMemory.Run);
        Assert.False(properties.ContainsKey(WorkflowsConstants.AccessRunProperty));
    }

    [Fact]
    public void System_scope_flows_with_the_async_chain_and_ends_on_dispose()
    {
        Assert.False(WorkflowSystemScope.IsActive);
        using (WorkflowSystemScope.Begin())
        {
            Assert.True(WorkflowSystemScope.IsActive);
        }

        Assert.False(WorkflowSystemScope.IsActive);
    }

    private static ClaimsPrincipal User(string name, params string[] roles) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Name, name), .. roles.Select(r => new Claim(ClaimTypes.Role, r))], "Test", ClaimTypes.Name, ClaimTypes.Role));

    // The handler answers on the request's one caller: the gate would have built it from this
    // principal, with the super user flagged for the site's super user and the admin role.
    private static Grove.Access.ICallerContextAccessor CallerFor(ClaimsPrincipal user, string superUser)
    {
        var name = user.Identity?.Name ?? string.Empty;
        var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var accessor = Substitute.For<Grove.Access.ICallerContextAccessor>();
        accessor.Current.Returns(new Grove.Access.CallerContext
        {
            Tenant = "t",
            Side = Grove.Access.CallerSide.Admin,
            UserId = name,
            UserName = name,
            IsSuperUser = name == superUser || roles.Contains("Administrator"),
            Roles = roles,
        });
        return accessor;
    }

    /// <summary>Whether the lists veto the permission for the user, as the one decision's ceiling answers it.</summary>
    private static bool Denied(ClaimsPrincipal user, string permissionName, object resource, string superUser = "root")
        => new WorkflowDefinitionAccessCeiling().Deny(CallerFor(user, superUser).Current!, permissionName, resource) is not null;

    [Fact]
    public void A_definition_with_no_lists_changes_nothing()
    {
        Assert.False(Denied(User("alice", "Editor"), WorkflowsConstants.Permissions.Edit, new WorkflowDefinitionAccessResource("d", [], [])));
    }

    [Fact]
    public void Edit_permissions_are_vetoed_for_users_outside_the_edit_list_and_admitted_by_role_or_name()
    {
        var resource = new WorkflowDefinitionAccessResource("d", ["Finance", "bob"], []);

        Assert.True(Denied(User("alice", "Editor"), WorkflowsConstants.Permissions.Edit, resource));
        Assert.True(Denied(User("alice", "Editor"), WorkflowsConstants.Permissions.Publish, resource));
        Assert.True(Denied(User("alice", "Editor"), WorkflowsConstants.Permissions.ManageShipped, resource));
        Assert.False(Denied(User("alice", "Finance"), WorkflowsConstants.Permissions.Edit, resource));
        Assert.False(Denied(User("bob", "Editor"), WorkflowsConstants.Permissions.Edit, resource));
        // The edit list says nothing about running.
        Assert.False(Denied(User("alice", "Editor"), WorkflowsConstants.Permissions.Run, resource));
    }

    [Fact]
    public void The_super_user_and_administrators_are_never_narrowed()
    {
        var resource = new WorkflowDefinitionAccessResource("d", ["Finance"], ["Finance"]);

        Assert.False(Denied(User("root"), WorkflowsConstants.Permissions.Edit, resource));
        Assert.False(Denied(User("carol", "Administrator"), WorkflowsConstants.Permissions.Run, resource));
        Assert.True(Denied(User("dave", "Editor"), WorkflowsConstants.Permissions.Run, resource));
    }

    [Fact]
    public void Other_permissions_and_other_resources_are_ignored()
    {
        var resource = new WorkflowDefinitionAccessResource("d", ["Finance"], ["Finance"]);
        Assert.False(Denied(User("alice"), "ManageContent", resource));

        Assert.False(Denied(User("alice"), Permissions.EditWorkflows.Name, "not a definition"));
    }
}
