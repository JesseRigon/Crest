using Grove.Infrastructure.Shells.Admin;
using Grove.Routing;

namespace Grove.Workflows.Navigation;

/// <summary>
/// The Workflows section: definitions (list and designer), instances, hooks and
/// approvals, the forked Studio mounted in the Grove shell. The pages declare themselves on their
/// components; the section opens Approvals, which any admin user may have to decide, so it is
/// shown to everyone who can reach one of its pages.
/// </summary>
public sealed class WorkflowsPages : IPageSource
{
    public IEnumerable<PageEntry> GetPages() =>
    [
        new PageEntry
        {
            Id = "workflows",
            Permissions = [AdminPermissions.AccessAdminPanel.Name],
            Menu = new PageMenu("Workflows", Position: "70", Icon: "fa fa-project-diagram") { Url = WorkflowsConstants.Routes.ApprovalsAdmin },
        },
    ];
}
