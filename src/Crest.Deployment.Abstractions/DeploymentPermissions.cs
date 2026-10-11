using Crest.Security.Permissions;

namespace Crest.Deployment;

public static class DeploymentPermissions
{
    public static readonly Permission ManageDeploymentPlan = new("ManageDeploymentPlan", "Manage deployment plans");

    public static readonly Permission Export = new("Export", "Export Data");

    public static readonly Permission Import = new("Import", "Import Data", isSecurityCritical: true);

    public static readonly Permission ManageRemoteInstances = new("ManageRemoteInstances", "Manage remote instances");

    public static readonly Permission ManageRemoteClients = new("ManageRemoteClients", "Manage remote clients");

    public static readonly Permission ExportRemoteInstances = new("ExportRemoteInstances", "Export to remote instances");

    /// <summary>Held by a remote client's caller: import a package another instance sent.</summary>
    public static readonly Permission ImportRemoteInstances = new("ImportRemoteInstances", "Import from remote instances", isSecurityCritical: true);
}
