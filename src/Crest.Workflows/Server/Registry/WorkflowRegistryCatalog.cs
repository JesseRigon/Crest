namespace Crest.Workflows.Registry;

/// <summary>
/// The fifth registry's catalog, shaped like the Parties one: every provider's triggers and
/// flows merged by key (first registration wins), ordered by position then key.
/// </summary>
public sealed class WorkflowRegistryCatalog(
    IEnumerable<IWorkflowTriggerProvider> triggerProviders,
    IEnumerable<IWorkflowFlowProvider> flowProviders,
    IEnumerable<IWorkflowActivityProvider> activityProviders,
    IEnumerable<IWorkflowConnectorProvider> connectorProviders,
    IEnumerable<IWorkflowHookSlotProvider> hookSlotProviders,
    IEnumerable<IWorkflowHookAttachmentProvider> hookAttachmentProviders)
{
    // Merged on first use, not in the constructor: the stock activity provider lists the
    // stock activity library, whose activities' constructors reach content handlers that
    // raise triggers through the publisher, which takes this catalog. Building eagerly
    // would re-enter the library while it is still being built.
    private IReadOnlyList<WorkflowTriggerDescriptor>? _triggers;
    private IReadOnlyList<WorkflowFlowDescriptor>? _flows;
    private IReadOnlyList<WorkflowActivityDescriptor>? _activities;
    private IReadOnlyList<WorkflowConnectorDescriptor>? _connectors;
    private IReadOnlyList<WorkflowHookSlotDescriptor>? _hookSlots;
    private IReadOnlyList<WorkflowHookAttachmentDescriptor>? _hookAttachments;

    public IReadOnlyList<WorkflowTriggerDescriptor> Triggers => _triggers ??= Merge(triggerProviders.SelectMany(p => p.Triggers), t => t.Key, t => t.Position);
    public IReadOnlyList<WorkflowFlowDescriptor> Flows => _flows ??= Merge(flowProviders.SelectMany(p => p.Flows), f => f.Key, f => f.Position);
    public IReadOnlyList<WorkflowActivityDescriptor> Activities => _activities ??= Merge(activityProviders.SelectMany(p => p.Activities), a => a.Key, a => a.Position);

    public IReadOnlyList<WorkflowConnectorDescriptor> Connectors => _connectors ??= Merge(connectorProviders.SelectMany(p => p.Connectors), c => c.Key, c => c.Position);

    public IReadOnlyList<WorkflowHookSlotDescriptor> HookSlots => _hookSlots ??= Merge(hookSlotProviders.SelectMany(p => p.HookSlots), s => s.Key, s => s.Position);

    // System attachments: one flow per slot at most, keyed slot + flow; first registration wins.
    public IReadOnlyList<WorkflowHookAttachmentDescriptor> HookAttachments => _hookAttachments ??= Merge(hookAttachmentProviders.SelectMany(p => p.HookAttachments), a => $"{a.Slot}|{a.FlowKey}", a => a.Position);

    public WorkflowHookSlotDescriptor? FindHookSlot(string key) => HookSlots.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase));

    public WorkflowConnectorDescriptor? FindConnector(string key) => Connectors.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase));
    public WorkflowTriggerDescriptor? FindTrigger(string key) => Triggers.FirstOrDefault(t => string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase));
    public WorkflowFlowDescriptor? FindFlow(string key) => Flows.FirstOrDefault(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<T> Merge<T>(IEnumerable<T> items, Func<T, string> key, Func<T, int> position)
    {
        var seen = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            seen.TryAdd(key(item), item);
        }

        return seen.Values.OrderBy(position).ThenBy(key, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
