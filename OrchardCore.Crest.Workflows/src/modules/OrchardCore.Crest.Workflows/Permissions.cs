using OrchardCore.Security.Permissions;
using OrchardCore.Workflows;

namespace OrchardCore.Crest.Workflows;

/// <summary>
/// The gate on the Elsa API is the stock module's own <c>ManageWorkflows</c> permission
/// (declared once, by <c>OrchardCore.Workflows</c>, which this feature depends on), so
/// roles configured for workflows carry over and no second permission with the same name
/// exists. Administrator only by default: an activity runs as trusted system code once a
/// definition is published, so authoring is the security boundary.
/// </summary>
public static class Permissions
{
    public static readonly Permission ManageWorkflows = WorkflowsPermissions.ManageWorkflows;
}
