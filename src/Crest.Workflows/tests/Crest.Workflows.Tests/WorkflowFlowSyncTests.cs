using Crest.Workflows.Registry;
using Xunit;

namespace Crest.Workflows.Tests;

// What a sync does per shipped flow (docs/workflows.md › Registry): install, re-publish a
// system flow that lost its published version, upgrade an unforked copy behind the shipped
// version, keep a forked one - the runtime path the activation task and the sync endpoint share.
public class WorkflowFlowSyncTests
{
    private static WorkflowFlowDescriptor Shipped(int version, string ownership = WorkflowOwnership.Shipped) =>
        new("mod.flow", "Flow", "transaction", "{}", 0, Ownership: ownership, Version: version);

    private static WorkflowFlowInstallation Installed(int? version, bool forked = false, bool published = true, string ownership = WorkflowOwnership.Shipped) =>
        new("def", published, new WorkflowOwnershipInfo(ownership, "mod.flow", version, forked));

    [Fact]
    public void Missing_is_installed() => Assert.Equal(WorkflowFlowSyncAction.Install, WorkflowFlowImporter.Decide(Shipped(1), null));

    [Fact]
    public void Unpublished_system_flow_is_republished() =>
        Assert.Equal(WorkflowFlowSyncAction.Republish, WorkflowFlowImporter.Decide(Shipped(1, WorkflowOwnership.System), Installed(1, published: false, ownership: WorkflowOwnership.System)));

    [Fact]
    public void Unpublished_shipped_flow_is_kept_unpublished() =>
        Assert.Equal(WorkflowFlowSyncAction.Keep, WorkflowFlowImporter.Decide(Shipped(1), Installed(1, published: false)));

    [Fact]
    public void Version_bump_upgrades_an_unforked_copy() =>
        Assert.Equal(WorkflowFlowSyncAction.Upgrade, WorkflowFlowImporter.Decide(Shipped(2), Installed(1)));

    [Fact]
    public void Missing_installed_version_counts_as_one() =>
        Assert.Equal(WorkflowFlowSyncAction.Upgrade, WorkflowFlowImporter.Decide(Shipped(2), Installed(null)));

    [Fact]
    public void Version_bump_leaves_a_forked_copy_alone() =>
        Assert.Equal(WorkflowFlowSyncAction.Keep, WorkflowFlowImporter.Decide(Shipped(2), Installed(1, forked: true)));

    [Fact]
    public void Current_copy_is_kept() =>
        Assert.Equal(WorkflowFlowSyncAction.Keep, WorkflowFlowImporter.Decide(Shipped(1), Installed(1)));

    [Fact]
    public void System_flow_behind_is_upgraded_even_when_forked_flag_is_absent() =>
        Assert.Equal(WorkflowFlowSyncAction.Upgrade, WorkflowFlowImporter.Decide(Shipped(3, WorkflowOwnership.System), Installed(2, ownership: WorkflowOwnership.System)));
}
