using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Services;

namespace Crest.Workflows.Orchard;

/// <summary>
/// Every stock OrchardCore activity the tenant's enabled modules registered, as registry
/// entries: an event is the <see cref="OrchardEvent"/> trigger, a task the
/// <see cref="OrchardTask"/> activity, each with its stock name preset. Activities the
/// engine does natively (<see cref="StockActivityRunner.EngineNative"/>) are left out.
/// Disabling a module removes its activities from the stock library, and so from here.
/// </summary>
public sealed class StockActivityProvider(IActivityLibrary activityLibrary) : IWorkflowActivityProvider
{
    public const string KeyPrefix = "orchard.";

    public IEnumerable<WorkflowActivityDescriptor> Activities =>
        activityLibrary.ListActivities()
            .Where(activity => !StockActivityRunner.EngineNative.Contains(activity.Name))
            .Select(activity =>
            {
                var isEvent = activity is IEvent;
                // A task with an effect outside the database runs after commit (OrchardExternalTask).
                var adapter = isEvent ? typeof(OrchardEvent) : StockActivityRunner.External.Contains(activity.Name) ? typeof(OrchardExternalTask) : typeof(OrchardTask);
                var nameInput = isEvent ? nameof(OrchardEvent.EventName) : nameof(OrchardTask.ActivityName);
                return new WorkflowActivityDescriptor(
                    KeyPrefix + activity.Name,
                    activity.DisplayText.Value,
                    WorkflowsConstants.Objects.Orchard,
                    $"Crest.Workflows.{adapter.Name}",
                    Category: activity.Category.Value,
                    Position: 1000,
                    IsTrigger: isEvent,
                    Inputs: new Dictionary<string, string> { [ToCamelCase(nameInput)] = activity.Name });
            });

    private static string ToCamelCase(string name) => char.ToLowerInvariant(name[0]) + name[1..];
}
