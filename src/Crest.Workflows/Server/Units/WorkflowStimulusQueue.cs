using System.Text.Json;
using Crest.Access;
using Crest.Data.Documents;
using Crest.Data.Migration;
using Crest.Environment.Shell.Scope;
using Crest.Workflows.Contexts;
using Crest.Workflows.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YesSql;
using YesSql.Indexes;
using YesSql.Sql;

namespace Crest.Workflows.Units;

public static class WorkflowStimulusStatuses
{
    /// <summary>Written by the raising unit; not yet delivered.</summary>
    public const string Pending = "pending";
    /// <summary>Sent; the flows it started or resumed committed with this mark (or recorded their failure).</summary>
    public const string Delivered = "delivered";
    /// <summary>The send itself threw <see cref="WorkflowStimulusQueue.MaxAttempts"/> times: set aside so the object's later events are not held forever.</summary>
    public const string Failed = "failed";
}

/// <summary>
/// A stimulus in the event queue (docs/workflows.md › Queues, the event side): written in the
/// unit that raised it, so it exists only if that unit committed; delivered after the commit
/// in its object's partition (the correlation id), in order, one consumer at a time; the mark
/// that it was delivered commits with the flows the delivery ran, so a delivery is seen once.
/// </summary>
public sealed class WorkflowStimulus
{
    public long Id { get; set; }

    /// <summary>The idempotency key: the raising unit's id and the stimulus's slot in it.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The partition: the correlation id, or the key itself for an uncorrelated stimulus.</summary>
    public string Partition { get; set; } = string.Empty;

    public string ActivityTypeName { get; set; } = string.Empty;

    public WorkflowStimulusValue Stimulus { get; set; } = new();

    public string? CorrelationId { get; set; }

    public string? WorkflowInstanceId { get; set; }

    public string? ActivityInstanceId { get; set; }

    public string? BookmarkId { get; set; }

    public Dictionary<string, WorkflowStimulusValue> Input { get; set; } = [];

    public WorkflowUserContext? ActAs { get; set; }

    public string Status { get; set; } = WorkflowStimulusStatuses.Pending;

    public int Attempts { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime? DeliveredUtc { get; set; }

    public string? Error { get; set; }
}

/// <summary>One value as written: its CLR type and its JSON, so the delivery hands the flows the
/// same types the raiser passed (a content item stays a content item).</summary>
public sealed class WorkflowStimulusValue
{
    public string? Type { get; set; }

    public JsonElement Json { get; set; }

    public static WorkflowStimulusValue From(object? value) => value is null
        ? new()
        : new() { Type = value.GetType().AssemblyQualifiedName, Json = JsonSerializer.SerializeToElement(value, value.GetType(), JOptions.Default) };

    public object? ToValue()
    {
        if (Type is null || Json.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return null;
        }

        var type = System.Type.GetType(Type);
        if (type is null || type == typeof(object) || typeof(IDictionary<string, object>).IsAssignableFrom(type) && type.IsGenericType)
        {
            return Plain(Json);
        }

        return Json.Deserialize(type, JOptions.Default);
    }

    /// <summary>A JSON value as the plain CLR values a payload is made of (ids and scalars).</summary>
    public static object? Plain(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var integer) ? (object)integer : element.GetDecimal(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Object => element.EnumerateObject().ToDictionary(property => property.Name, property => Plain(property.Value)!, StringComparer.Ordinal),
        JsonValueKind.Array => element.EnumerateArray().Select(Plain).ToList(),
        _ => null,
    };
}

public sealed class WorkflowStimulusIndex : MapIndex
{
    public string Key { get; set; } = string.Empty;
    public string Partition { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    /// <summary>The document id: the order stimuli were written in.</summary>
    public long Sequence { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public sealed class WorkflowStimulusIndexProvider : IndexProvider<WorkflowStimulus>
{
    public override void Describe(DescribeContext<WorkflowStimulus> context) =>
        context.For<WorkflowStimulusIndex>().Map(stimulus => new WorkflowStimulusIndex
        {
            Key = stimulus.Key,
            Partition = stimulus.Partition,
            Status = stimulus.Status,
            Sequence = stimulus.Id,
            CreatedUtc = stimulus.CreatedUtc,
        });
}

public sealed class WorkflowStimulusMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<WorkflowStimulusIndex>(table => table
            .Column<string>(nameof(WorkflowStimulusIndex.Key), c => c.NotNull().WithLength(80))
            .Column<string>(nameof(WorkflowStimulusIndex.Partition), c => c.NotNull().WithLength(255))
            .Column<string>(nameof(WorkflowStimulusIndex.Status), c => c.NotNull().WithLength(16))
            .Column<long>(nameof(WorkflowStimulusIndex.Sequence))
            .Column<DateTime>(nameof(WorkflowStimulusIndex.CreatedUtc)));

        await SchemaBuilder.AlterIndexTableAsync<WorkflowStimulusIndex>(table =>
        {
            table.CreateIndex("IDX_WorkflowStimulusIndex_Key", nameof(WorkflowStimulusIndex.Key));
            table.CreateIndex("IDX_WorkflowStimulusIndex_Partition", nameof(WorkflowStimulusIndex.Partition), nameof(WorkflowStimulusIndex.Status));
            table.CreateIndex("IDX_WorkflowStimulusIndex_Status", nameof(WorkflowStimulusIndex.Status), nameof(WorkflowStimulusIndex.CreatedUtc));
        });

        return 1;
    }
}

/// <summary>
/// The event queue (docs/workflows.md › Queues, the event side), on the engine's stimulus
/// dispatch: triggers fire after commit, never inside the raiser's transaction. Every
/// stimulus a registry, a content event or a flow raises is written in the raising unit
/// (<see cref="WorkflowStimulus"/>, keyed by the unit's id and the stimulus's slot in it), so
/// it exists only if that unit commits: a hooked flow only ever sees committed state and no
/// event exists for an invoice that never made it. After the commit the stimulus's partition
/// (its correlation id) is drained: each delivery is one child scope - one burst, one unit -
/// that takes the object's lock, takes the partition's oldest pending stimulus, sends it and
/// marks it delivered, the mark committing with the flows the send ran and the lock held
/// until that commit. So one object's events are consumed one at a time and in order, and a
/// stimulus is delivered once; one left pending by a process that died is delivered again
/// when the tenant activates (at least once, de-duplicated by its key). Outside a shell scope
/// (unit tests) the send is immediate.
/// </summary>
public sealed class WorkflowStimulusQueue(
    IStimulusSender stimulusSender,
    WorkflowAfterCommit afterCommit,
    ILogger<WorkflowStimulusQueue> logger,
    WorkflowUnitOfWork? unit = null,
    ISession? session = null)
{
    /// <summary>Sends that threw before a stimulus is set aside as failed.</summary>
    public const int MaxAttempts = 3;

    public Task EnqueueAsync<TActivity>(object stimulus, StimulusMetadata metadata, WorkflowUserContext? actAs = null) where TActivity : IActivity =>
        EnqueueAsync(Helpers.ActivityTypeNameHelper.GenerateTypeName<TActivity>(), stimulus, metadata, actAs);

    /// <param name="actAs">The user the event names, when the flows it starts act as that user
    /// rather than as the caller that queued it (a sign-in, a registration: docs/access.md,
    /// Anonymous actors). Built into a caller with current rights in the delivery scope, after
    /// commit, so a user created by the unit is there to be read.</param>
    public async Task EnqueueAsync(string activityTypeName, object stimulus, StimulusMetadata metadata, WorkflowUserContext? actAs = null)
    {
        // The event side's idempotency key: the raising unit and the stimulus's slot in it, in
        // the input of every flow it starts or resumes.
        var key = unit is null ? Guid.NewGuid().ToString("n") : $"{unit.Id}:{unit.NextSlot()}";
        metadata.Input ??= new Dictionary<string, object>();
        metadata.Input[WorkflowsConstants.InputKeys.StimulusId] = key;

        if (ShellScope.Current is null || session is null)
        {
            await stimulusSender.SendAsync(activityTypeName, stimulus, metadata);
            return;
        }

        var record = new WorkflowStimulus
        {
            Key = key,
            Partition = metadata.CorrelationId is { Length: > 0 } correlation ? correlation : key,
            ActivityTypeName = activityTypeName,
            Stimulus = WorkflowStimulusValue.From(stimulus),
            CorrelationId = metadata.CorrelationId,
            WorkflowInstanceId = metadata.WorkflowInstanceId,
            ActivityInstanceId = metadata.ActivityInstanceId,
            BookmarkId = metadata.BookmarkId,
            Input = metadata.Input.ToDictionary(pair => pair.Key, pair => WorkflowStimulusValue.From(pair.Value), StringComparer.Ordinal),
            ActAs = actAs,
            CreatedUtc = DateTime.UtcNow,
        };
        await session.SaveAsync(record);

        var partition = record.Partition;
        afterCommit.Enqueue($"stimulus {activityTypeName} for {metadata.CorrelationId}", () => DrainAsync(partition));
    }

    /// <summary>
    /// Delivers a partition's pending stimuli, oldest first, until none is left. Run after the
    /// raising unit commits (and at activation for what a dead process left); several drains of
    /// one partition may run at once - each delivery takes the oldest under the object's lock,
    /// so the order holds whoever delivers.
    /// </summary>
    public static async Task DrainAsync(string partition)
    {
        while (await DeliverNextAsync(partition))
        {
        }
    }

    /// <summary>Stimuli left pending (a process that died between a commit and its delivery): delivered again, partition by partition. Called when the tenant activates, inside a shell scope.</summary>
    public static async Task<int> RecoverAsync(ISession session)
    {
        var partitions = (await session.QueryIndex<WorkflowStimulusIndex>(index => index.Status == WorkflowStimulusStatuses.Pending).ListAsync())
            .Select(index => index.Partition)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (var partition in partitions)
        {
            await DrainAsync(partition);
        }

        return partitions.Count;
    }

    /// <summary>One delivery, as its own unit. False when the partition has nothing pending (or the delivery threw: it stays pending, retried later).</summary>
    private static async Task<bool> DeliverNextAsync(string partition)
    {
        string? key = null;
        Exception? failure = null;
        var unitFailed = false;

        // The parent scope activated the shell; a child never does (that path takes the
        // activation lock, which during setup is held by the scope this task belongs to).
        await ShellScope.UsingChildScopeAsync(async child =>
        {
            var services = child.ServiceProvider;

            // One unit per object at a time, held until this scope has committed: the flows the
            // stimulus starts about the object run under its lock (the write side's
            // serialization), and no other delivery reads the stimulus before its mark is in.
            var held = await services.GetRequiredService<IWorkflowObjectLock>().LockAsync(partition);
            ShellScope.RegisterBeforeDispose(_ => held.DisposeAsync().AsTask(), last: true);

            var session = services.GetRequiredService<ISession>();
            var record = await session.Query<WorkflowStimulus, WorkflowStimulusIndex>(index => index.Partition == partition && index.Status == WorkflowStimulusStatuses.Pending)
                .OrderBy(index => index.Sequence)
                .FirstOrDefaultAsync();
            if (record is null)
            {
                return;
            }

            key = record.Key;
            var metadata = new StimulusMetadata
            {
                CorrelationId = record.CorrelationId,
                WorkflowInstanceId = record.WorkflowInstanceId,
                ActivityInstanceId = record.ActivityInstanceId,
                BookmarkId = record.BookmarkId,
                Input = record.Input.ToDictionary(pair => pair.Key, pair => pair.Value.ToValue()!, StringComparer.Ordinal),
            };
            var stimulus = record.Stimulus.ToValue() ?? throw new InvalidOperationException($"Stimulus {record.Key} could not be read back ({record.Stimulus.Type}).");

            try
            {
                var sender = services.GetRequiredService<IStimulusSender>();
                var result = record.ActAs is null
                    ? await sender.SendAsync(record.ActivityTypeName, stimulus, metadata)
                    : await services.GetRequiredService<IAccessRunner>().RunAsAsync(
                        await services.GetRequiredService<WorkflowCallerResolver>().FromActorAsync(record.ActAs),
                        () => sender.SendAsync(record.ActivityTypeName, stimulus, metadata));
                services.GetRequiredService<ILogger<WorkflowStimulusQueue>>().LogDebug("Stimulus {Key} ({Activity}) delivered for {Correlation}: {Count} workflow(s) started or resumed.", record.Key, record.ActivityTypeName, record.CorrelationId, result.WorkflowInstanceResponses.Count);
            }
            catch (Exception ex)
            {
                // Nothing the send did stands; the attempt is recorded below, in a scope of its own.
                failure = ex;
                await services.GetRequiredService<IDocumentStore>().CancelAsync();
                return;
            }

            unitFailed = services.GetRequiredService<WorkflowUnitOfWork>().IsFailed;
            record.Status = WorkflowStimulusStatuses.Delivered;
            record.DeliveredUtc = DateTime.UtcNow;
            record.Attempts++;
            await session.SaveAsync(record);
        }, activateShell: false);

        if (key is null)
        {
            return false;
        }

        if (failure is null && !unitFailed)
        {
            return true;
        }

        // A unit the delivered flows failed is discarded with its mark: the flows' failure is
        // recorded on their instances, so the stimulus counts as delivered, not as pending. A
        // send that threw is retried until it has thrown MaxAttempts times.
        var deliveredKey = key;
        var retry = false;
        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            var session = scope.ServiceProvider.GetRequiredService<ISession>();
            var record = await session.Query<WorkflowStimulus, WorkflowStimulusIndex>(index => index.Key == deliveredKey).FirstOrDefaultAsync();
            if (record is null || record.Status != WorkflowStimulusStatuses.Pending)
            {
                return;
            }

            record.Attempts++;
            if (failure is null)
            {
                record.Status = WorkflowStimulusStatuses.Delivered;
                record.DeliveredUtc = DateTime.UtcNow;
                record.Error = "The delivered flows failed their unit.";
            }
            else
            {
                record.Error = failure.Message;
                record.Status = record.Attempts >= MaxAttempts ? WorkflowStimulusStatuses.Failed : WorkflowStimulusStatuses.Pending;
                retry = record.Status == WorkflowStimulusStatuses.Pending;
                scope.ServiceProvider.GetRequiredService<ILogger<WorkflowStimulusQueue>>().LogError(failure, "Stimulus {Key} ({Activity}) for {Correlation} failed to deliver (attempt {Attempt}).", record.Key, record.ActivityTypeName, record.CorrelationId, record.Attempts);
            }

            await session.SaveAsync(record);
        }, activateShell: false);

        // A send that will be retried holds the partition: its later events wait for it, at the
        // next drain (the next event about the object, or the next activation).
        return failure is null || !retry;
    }
}
