using System.Security.Claims;
using System.Text.Json;
using Crest.Workflows.Registry;
using Crest.Workflows.Security;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using OrchardCore.Security;
using OrchardCore.Settings;
using Xunit;

namespace Crest.Workflows.Tests;

// Phase 5 (docs/workflows.md): ownership properties round-trip through the stored JSON
// shape, the tier rules, and the access handler's place in the authorization pipeline.
public class WorkflowOwnershipAndAccessTests
{
    private static IDictionary<string, object> Stored(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, object>>(json)!;

    [Fact]
    public void Ownership_reads_the_stored_json_shape_and_defaults_to_tenant()
    {
        var info = WorkflowOwnershipInfo.From(Stored("""{"Crest.Ownership":"shipped","Crest.FlowKey":"accounting.quote-to-invoice","Crest.FlowVersion":3,"Crest.Forked":true}"""));

        Assert.True(info.IsShipped);
        Assert.Equal("accounting.quote-to-invoice", info.FlowKey);
        Assert.Equal(3, info.FlowVersion);
        Assert.True(info.Forked);

        Assert.True(WorkflowOwnershipInfo.From(Stored("""{"other":1}""")).IsTenant);
        Assert.True(WorkflowOwnershipInfo.From(null).IsTenant);
        Assert.True(WorkflowOwnershipInfo.From(Stored("""{"Crest.Ownership":"SYSTEM"}""")).IsSystem);
    }

    [Fact]
    public void Stamp_replaces_whatever_the_client_sent()
    {
        var properties = Stored("""{"Crest.Ownership":"tenant","Crest.Forked":false,"designer":{"x":1}}""");
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
        var fromJson = WorkflowDefinitionAccessResource.From("d", Stored("""{"Crest.Access.Edit":["Editor"," alice "],"Crest.Access.Run":"Operators, bob"}"""));
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

    private static async Task<AuthorizationHandlerContext> HandleAsync(ClaimsPrincipal user, string permissionName, WorkflowDefinitionAccessResource resource, string superUser = "root")
    {
        var siteService = Substitute.For<ISiteService>();
        var settings = Substitute.For<ISite>();
        settings.SuperUser.Returns(superUser);
        siteService.GetSiteSettingsAsync().Returns(settings);
        var handler = new WorkflowDefinitionAccessHandler(siteService);
        var context = new AuthorizationHandlerContext([new PermissionRequirement(new OrchardCore.Security.Permissions.Permission(permissionName))], user, resource);
        await handler.HandleAsync(context);
        return context;
    }

    [Fact]
    public async Task A_definition_with_no_lists_changes_nothing()
    {
        var context = await HandleAsync(User("alice", "Editor"), WorkflowsConstants.Permissions.Edit, new("d", [], []));
        Assert.False(context.HasFailed);
    }

    [Fact]
    public async Task Edit_permissions_are_vetoed_for_users_outside_the_edit_list_and_admitted_by_role_or_name()
    {
        var resource = new WorkflowDefinitionAccessResource("d", ["Finance", "bob"], []);

        Assert.True((await HandleAsync(User("alice", "Editor"), WorkflowsConstants.Permissions.Edit, resource)).HasFailed);
        Assert.True((await HandleAsync(User("alice", "Editor"), WorkflowsConstants.Permissions.Publish, resource)).HasFailed);
        Assert.True((await HandleAsync(User("alice", "Editor"), WorkflowsConstants.Permissions.ManageShipped, resource)).HasFailed);
        Assert.False((await HandleAsync(User("alice", "Finance"), WorkflowsConstants.Permissions.Edit, resource)).HasFailed);
        Assert.False((await HandleAsync(User("bob", "Editor"), WorkflowsConstants.Permissions.Edit, resource)).HasFailed);
        // The edit list says nothing about running.
        Assert.False((await HandleAsync(User("alice", "Editor"), WorkflowsConstants.Permissions.Run, resource)).HasFailed);
    }

    [Fact]
    public async Task The_super_user_and_administrators_are_never_narrowed()
    {
        var resource = new WorkflowDefinitionAccessResource("d", ["Finance"], ["Finance"]);

        Assert.False((await HandleAsync(User("root"), WorkflowsConstants.Permissions.Edit, resource)).HasFailed);
        Assert.False((await HandleAsync(User("carol", "Administrator"), WorkflowsConstants.Permissions.Run, resource)).HasFailed);
        Assert.True((await HandleAsync(User("dave", "Editor"), WorkflowsConstants.Permissions.Run, resource)).HasFailed);
    }

    [Fact]
    public async Task Other_permissions_and_other_resources_are_ignored()
    {
        var resource = new WorkflowDefinitionAccessResource("d", ["Finance"], ["Finance"]);
        Assert.False((await HandleAsync(User("alice"), "ManageContent", resource)).HasFailed);

        var siteService = Substitute.For<ISiteService>();
        var context = new AuthorizationHandlerContext([new PermissionRequirement(Permissions.EditWorkflows)], User("alice"), "not a definition");
        await new WorkflowDefinitionAccessHandler(siteService).HandleAsync(context);
        Assert.False(context.HasFailed);
    }
}
