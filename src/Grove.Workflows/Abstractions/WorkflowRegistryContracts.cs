namespace Grove.Workflows;

/// <summary>
/// A trigger a registry can raise: <c>order.placed</c>, <c>party.role-created</c>, ...
/// Modules that own the object declare it; flows subscribe to it by key through the
/// RegistryTrigger activity. The payload is ids and scalars only — activities re-read the
/// objects through their registries' services.
/// </summary>
public sealed record WorkflowTriggerDescriptor(string Key, string DisplayName, string Object, string? Description = null, int Position = 0);

/// <summary>
/// A workflow definition a module ships as data: the engine's definition-model JSON (the
/// export format), imported once per tenant when the feature activates and found again by
/// <see cref="Key"/>. <see cref="Ownership"/> is the tier (<see cref="WorkflowOwnership"/>):
/// a <c>system</c> flow is code's, read-only for every user and always published; a
/// <c>shipped</c> flow is a template the tenant may edit (the first edit forks it; the
/// shipped JSON stays the reset point). <see cref="Version"/> is bumped when the shipped
/// JSON changes: an unforked copy is upgraded to it at the next activation.
/// <see cref="Publish"/> false ships a <c>shipped</c> flow as a draft the tenant opts into
/// by publishing it: anything that changes business data on its own (converting documents,
/// creating roles) ships that way. A <c>system</c> flow is always published.
/// </summary>
public sealed record WorkflowFlowDescriptor(
    string Key,
    string DisplayName,
    string Object,
    string DefinitionJson,
    int Position = 0,
    bool Publish = true,
    string Ownership = WorkflowOwnership.Shipped,
    int Version = 1);

/// <summary>
/// Who owns a workflow definition (custom property <see cref="WorkflowsConstants.OwnershipProperty"/>).
/// Absent means <see cref="Tenant"/>: a flow a tenant user created.
/// </summary>
public static class WorkflowOwnership
{
    /// <summary>Code's. No user can save, publish, retract or delete it; the designer opens it read-only.</summary>
    public const string System = "system";
    /// <summary>A shipped template a tenant may edit (Manage shipped workflows), fork and reset.</summary>
    public const string Shipped = "shipped";
    /// <summary>The tenant's own.</summary>
    public const string Tenant = "tenant";

    public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        System => System,
        Shipped => Shipped,
        _ => Tenant,
    };
}

/// <summary>
/// An activity a module contributes to the palette for the core object it owns (post a
/// transaction, create a party role, ...). <see cref="ActivityType"/> is the engine's
/// activity type name (namespace + class, e.g. <c>MyModule.PostTransaction</c>);
/// the activity class itself lives in the owning module and is registered with the engine
/// from that module's startup. <see cref="Inputs"/> presets the node's inputs when the
/// palette entry is one generic activity configured for a purpose (a stock Grove task is
/// the <c>PlatformTask</c> activity with its <c>ActivityName</c> preset).
/// </summary>
public sealed record WorkflowActivityDescriptor(
    string Key,
    string DisplayName,
    string Object,
    string ActivityType,
    string? Description = null,
    int Position = 0,
    string? Category = null,
    bool IsTrigger = false,
    IReadOnlyDictionary<string, string>? Inputs = null,
    IReadOnlyList<WorkflowFieldDependencyDescriptor>? FieldDependencies = null);

/// <summary>
/// A content field an activity reads or writes (docs/workflows.md › Posting on workflows:
/// contracts are per-activity field dependencies, not a record-level contract). The path is
/// <c>FieldSet.Field</c>; <see cref="Required"/> is the per-field marker - a required field that
/// is empty at runtime ends the activity on Failed with the field named, an optional one
/// reads null. <see cref="ContentType"/> narrows the check to one type; null means any type
/// carrying the part. <see cref="Writes"/> marks a field the activity writes rather than
/// reads: it must exist, but the tenant's required/optional choice for it does not matter.
/// Declared in code on system activities, derived from their bindings on configurable ones
/// (Copy fields' mapping rows).
/// </summary>
public sealed record WorkflowFieldDependencyDescriptor(string Path, bool Required = true, string? ContentType = null, string? Description = null, bool Writes = false);

/// <summary>
/// Declares a content field an activity class depends on: system activities name every
/// field they read, each with its required marker, and those fields are locked in the
/// shipped definitions. Read by the engine's descriptor modifier (so the palette and the
/// registry show them), by the publish-time analyzer, and at runtime through
/// <see cref="IWorkflowFieldDependencyChecker"/>. Pure declaration, so any module's
/// activity can carry it without referencing the engine module.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class FieldDependencyAttribute(string path) : Attribute
{
    public string Path { get; } = path;
    public bool Required { get; init; } = true;
    public string? ContentType { get; init; }
    public string? Description { get; init; }
    /// <summary>The activity writes this field (it must exist; required-or-optional is the tenant's).</summary>
    public bool Writes { get; init; }

    public WorkflowFieldDependencyDescriptor ToDescriptor() => new(Path, Required, ContentType, Description, Writes);
}

/// <summary>
/// An activity whose dependencies come from its configuration rather than its class (Copy
/// fields reads them off its mapping rows). Only literal inputs can be read at publish; an
/// expression-bound mapping declares nothing and is checked at runtime only.
/// </summary>
public interface IFieldDependencySource
{
    IEnumerable<WorkflowFieldDependencyDescriptor> GetFieldDependencies();
}

/// <summary>
/// The runtime side of the ruling: before an activity acts on an item, its declared
/// required fields must hold a value; the first empty one is the reason the activity ends
/// on Failed, with the field named. Optional dependencies are not checked - the activity
/// reads null. Null result: every required field has a value (or the item was not found,
/// which is the activity's own failure to report).
/// </summary>
public interface IWorkflowFieldDependencyChecker
{
    Task<string?> CheckAsync(object activity, string contentItemId, CancellationToken cancellationToken = default);
}

public static class WorkflowFieldDependencyStatuses
{
    public const string Ok = "ok";
    /// <summary>No content type carries the part.</summary>
    public const string MissingFieldSet = "missing-field-set";
    /// <summary>The part exists but has no such field.</summary>
    public const string MissingField = "missing-field";
    /// <summary>The activity requires the field but the tenant's definition does not.</summary>
    public const string Optional = "optional";
    /// <summary>The field is shown only under a Grove visibility condition.</summary>
    public const string Conditional = "conditional";
}

/// <summary>One dependency of one node, as resolved against the tenant's definitions.</summary>
public sealed record WorkflowFieldDependencyBinding(
    string ActivityId,
    string ActivityType,
    string Path,
    bool Required,
    bool Writes,
    string? ContentType,
    string Status,
    string? FieldType,
    string? InDefinition);

/// <summary>A publish-time warning: never a refusal.</summary>
public sealed record WorkflowFieldDependencyWarning(string ActivityId, string ActivityType, string Path, string Status, string Message, string? InDefinition);

public sealed record WorkflowFieldDependencyReport(IReadOnlyList<WorkflowFieldDependencyBinding> Dependencies, IReadOnlyList<WorkflowFieldDependencyWarning> Warnings);

/// <summary>
/// A hook slot (docs/workflows.md › Posting on workflows): a named point inside a flow where
/// attached flows run <em>inline, inside the same unit of work</em> - the invoice and what the
/// tenant attached to its creation exist together or not at all. Declared by the module
/// that owns the flow raising it. <see cref="AllowTenantAttachments"/> false makes the slot
/// system-only; <see cref="RequiredOnly"/> true forbids best-effort tenant attachments (a
/// failure always fails the unit). Attachments must be atomic: no waits, no external calls.
/// </summary>
public sealed record WorkflowHookSlotDescriptor(
    string Key,
    string DisplayName,
    string Object,
    string? Description = null,
    int Position = 0,
    bool AllowTenantAttachments = true,
    bool RequiredOnly = false);

/// <summary>
/// A system attachment: the owning module attaches one of its shipped flows (by flow key) to
/// a slot. Carried by code like a system flow: tenants cannot remove, reorder or re-mark it.
/// </summary>
public sealed record WorkflowHookAttachmentDescriptor(string Slot, string FlowKey, int Position = 0, bool Required = true);

public interface IWorkflowHookSlotProvider
{
    IEnumerable<WorkflowHookSlotDescriptor> HookSlots { get; }
}

/// <summary>
/// Hook slots that come from tenant data rather than code: the content lifecycle's slots, one per
/// content type and lifecycle point (docs/workflows.md › Hook slots). Read when asked, so a type
/// created at runtime has its slots at once; a source also resolves the system attachments of
/// its slots.
/// </summary>
public interface IWorkflowHookSlotSource
{
    Task<IReadOnlyList<WorkflowHookSlotDescriptor>> ListHookSlotsAsync(CancellationToken cancellationToken = default);

    /// <summary>The slot with this key, or null when the source has none.</summary>
    Task<WorkflowHookSlotDescriptor?> FindHookSlotAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>The system attachments of one of the source's slots.</summary>
    Task<IReadOnlyList<WorkflowHookAttachmentDescriptor>> ListHookAttachmentsAsync(WorkflowHookSlotDescriptor slot, CancellationToken cancellationToken = default);

    /// <summary>
    /// A stamp of what the source's slots and attachments are read from (the content definitions'
    /// identifier): a slot's resolved attachments are cached until it changes.
    /// </summary>
    Task<string> StampAsync(CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
}

/// <summary>
/// A system attachment declared against a field set: the owning module attaches one of its
/// shipped flows (by flow key) to a content lifecycle point (<c>publishing</c>, ...) of every type
/// that attaches the field set, so the field set and its system flows attach and detach together
/// (docs/content-engine.md › Behaviour is workflows). <see cref="FieldSet"/> is the field set's
/// stored name.
/// </summary>
public sealed record WorkflowFieldSetHookAttachmentDescriptor(string FieldSet, string Point, string FlowKey, int Position = 0, bool Required = true);

public interface IWorkflowFieldSetHookAttachmentProvider
{
    IEnumerable<WorkflowFieldSetHookAttachmentDescriptor> FieldSetHookAttachments { get; }
}

/// <summary>
/// The field sets a type's items carry beyond the type's attachments: a field set a module welds on
/// them (a contained item's <c>ParentContentItem</c>). Their system flows run on the type's lifecycle
/// as an attached field set's do.
/// </summary>
public interface IWorkflowWeldedFieldSetSource
{
    Task<IReadOnlyCollection<string>> FieldSetsAsync(string contentType, CancellationToken cancellationToken = default);
}

public interface IWorkflowHookAttachmentProvider
{
    IEnumerable<WorkflowHookAttachmentDescriptor> HookAttachments { get; }
}

/// <summary>An attachment as the API shows it: system ones carry their flow key and no id.</summary>
public sealed record WorkflowHookAttachmentModel(
    string? Id,
    string Slot,
    string DefinitionId,
    string? DefinitionName,
    string? FlowKey,
    int Position,
    bool Required,
    string Ownership);

public sealed record WorkflowHookSlotModel(WorkflowHookSlotDescriptor Slot, IReadOnlyList<WorkflowHookAttachmentModel> Attachments);

/// <summary>A published flow the user may attach to a hook slot.</summary>
public sealed record WorkflowHookCandidateModel(string DefinitionId, string Name);

/// <summary>Attach a published definition to a slot; Required defaults to true.</summary>
public sealed record WorkflowHookAttachRequest(string Slot, string DefinitionId, int? Position = null, bool Required = true);

public interface IWorkflowActivityProvider
{
    IEnumerable<WorkflowActivityDescriptor> Activities { get; }
}

public interface IWorkflowTriggerProvider
{
    IEnumerable<WorkflowTriggerDescriptor> Triggers { get; }
}

public interface IWorkflowFlowProvider
{
    IEnumerable<WorkflowFlowDescriptor> Flows { get; }
}

/// <summary>
/// The one call a registry service makes to reach workflows: raise a registered trigger,
/// correlated to the object it is about, with a scalar payload. The acting user is captured
/// by the implementation; the caller never references the engine.
/// </summary>
public interface IWorkflowTriggerPublisher
{
    /// <summary>
    /// Raises the trigger <em>after the current unit of work commits</em>: subscribed flows run
    /// in their own unit once the raiser's transaction is in, and not at all if it is cancelled.
    /// Returns once queued. A flow that must run inside the raiser's transaction is a hook
    /// attachment, not a trigger subscriber.
    /// </summary>
    Task PublishAsync(string triggerKey, string? correlationId, IDictionary<string, object>? payload = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// The same, for an event that names its user - a sign-in, a registration (docs/access.md,
    /// Anonymous actors): when the request's caller is anonymous, the flows act as
    /// <paramref name="userId"/> (the user just signed in, the user just created), never as the
    /// anonymous request; a signed-in caller (an administrator creating a user) stays the actor.
    /// </summary>
    Task PublishForUserAsync(string triggerKey, string? correlationId, string userId, string? userName, IDictionary<string, object>? payload = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// The other call a registry service makes: run a hook slot <em>inside the current unit of
/// work</em>, where the service is creating or changing the object - the attached flows'
/// writes commit with the service's or not at all. A required attachment that fails fails
/// the unit (the session is cancelled at scope end) and throws
/// <see cref="WorkflowHookFailedException"/>, which the caller lets through: an API answers
/// it with 409, a flow faults. Best-effort failures come back in the result.
/// </summary>
public interface IWorkflowHookRunner
{
    Task<WorkflowHookRunResult> RunAsync(string slotKey, string? correlationId, IDictionary<string, object>? payload = null, CancellationToken cancellationToken = default);
}

public sealed record WorkflowHookRunResult(int Ran, IReadOnlyList<string> BestEffortFailures);

/// <summary>
/// Per-object serialization (docs/workflows.md › Posting on workflows, the write side): a
/// unit that changes an object takes its lock first, so no two units touching one object
/// interleave - the engine's distributed lock keyed by the object's id (the correlation id of
/// the flows about it). Re-entrant within one async flow: a flow that already holds the
/// invoice's lock may call the service that takes it again. Registry services take it around
/// their own writes; the engine's after-commit send takes it around each correlated run.
/// </summary>
public interface IWorkflowObjectLock
{
    Task<IAsyncDisposable> LockAsync(string objectId, CancellationToken cancellationToken = default);
}

/// <summary>One error of a failed hook attachment: the field path it is about (<c>FieldSet.Field</c>,
/// <c>FieldSet</c>, or <c>""</c>) and the message. A content hook answers them as the error contract's map.</summary>
public sealed record WorkflowHookError(string Path, string Message);

/// <summary>A required hook attachment failed: the unit it ran in is failed and will not commit.</summary>
public sealed class WorkflowHookFailedException(string slot, string attachment, string reason, IReadOnlyList<WorkflowHookError>? errors = null)
    : Exception($"Hook '{slot}': required attachment '{attachment}' failed - {reason}.")
{
    public string Slot { get; } = slot;
    public string Attachment { get; } = attachment;
    public string Reason { get; } = reason;

    /// <summary>What the attachment refused, by field path; one error under <c>""</c> when it named no field.</summary>
    public IReadOnlyList<WorkflowHookError> Errors { get; } = errors is { Count: > 0 } ? errors : [new(string.Empty, reason)];
}

/// <summary>
/// What the registry API reports for a shipped flow: where it landed, its tier, and whether
/// the tenant's copy is forked (edited) or outdated (an unreset fork of an older shipped
/// version; unforked copies never are, they upgrade at activation).
/// </summary>
public sealed record WorkflowFlowModel(
    string Key,
    string DisplayName,
    string Object,
    string? DefinitionId,
    bool Published,
    string Ownership,
    int ShippedVersion,
    int? InstalledVersion,
    bool Forked,
    bool Outdated);

public sealed record WorkflowRegistryModel(IReadOnlyList<WorkflowTriggerDescriptor> Triggers, IReadOnlyList<WorkflowActivityDescriptor> Activities, IReadOnlyList<WorkflowFlowModel> Flows, IReadOnlyList<WorkflowHookSlotDescriptor> HookSlots);

/// <summary>What is still pending for an object: the flows about it that have not finished, and the background jobs (external calls) they wait on.</summary>
public sealed record WorkflowPendingModel(string CorrelationId, IReadOnlyList<WorkflowPendingInstanceModel> Instances, IReadOnlyList<WorkflowPendingJobModel> Jobs)
{
    public bool IsPending => Instances.Count > 0 || Jobs.Count > 0;
}

public sealed record WorkflowPendingInstanceModel(string Id, string? DefinitionId, string? Name, string Status, string SubStatus, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

public sealed record WorkflowPendingJobModel(string JobId, string WorkflowInstanceId, string ActivityNodeId, string Status, int Attempts, DateTime CreatedUtc, DateTime? StartedUtc, string? Error);

/// <summary>A definition's access lists as the API shows and takes them (role names and user names): who may edit it, run it, and read its instances.</summary>
public sealed record WorkflowDefinitionAccessModel(IReadOnlyList<string> Edit, IReadOnlyList<string> Run, IReadOnlyList<string>? View = null)
{
    /// <summary>Who may read the definition's instances; empty = every View holder.</summary>
    public IReadOnlyList<string> View { get; init; } = View ?? [];
}

/// <summary>Reads a flow definition a module embeds under its <c>Data/workflows</c>.</summary>
public static class ShippedFlows
{
    public static string Read(System.Reflection.Assembly assembly, string fileName)
    {
        var name = assembly.GetManifestResourceNames().FirstOrDefault(candidate =>
                candidate.EndsWith(fileName, StringComparison.OrdinalIgnoreCase) && candidate.Contains("workflows", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"The embedded workflow definition '{fileName}' is missing from {assembly.GetName().Name}.");
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
