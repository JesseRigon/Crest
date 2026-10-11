namespace Crest.Workflows;

/// <summary>
/// Declares the permission an activity's execution takes (a data activity: it writes content,
/// calls a connection, runs a query). The engine's activity gate asks the one access decision
/// for it against the burst's caller before the activity runs and records the execution;
/// the registry carries it on the descriptor under
/// <see cref="WorkflowsConstants.RequiredPermissionDescriptorProperty"/>. An activity without
/// it declares no data access of its own.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class RequiresPermissionAttribute(string permission) : Attribute
{
    public string Permission { get; } = permission;
}
