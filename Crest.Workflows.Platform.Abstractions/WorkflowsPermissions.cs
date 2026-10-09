using Crest.Security.Permissions;

namespace Crest.Workflows.Platform;

public static class WorkflowsPermissions
{
    public static readonly Permission ManageWorkflows = new("ManageWorkflows", "Manage workflows", isSecurityCritical: true);
}
