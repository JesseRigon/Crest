using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Crest.Workflows.Platform.Abstractions.Models;
using Crest.Workflows.Platform.Activities;
using Crest.Workflows.Platform.Models;
using Crest.Workflows.Platform.Services;
// The engine and the stock module both define StockWorkflowExecutionContext, StockWorkflowStatus and
// StockActivity; the stock ones are meant everywhere in this file.
using StockActivity = Crest.Workflows.Platform.Activities.IActivity;
using StockWorkflowExecutionContext = Crest.Workflows.Platform.Models.WorkflowExecutionContext;
using StockWorkflowStatus = Crest.Workflows.Platform.Models.WorkflowStatus;

namespace Crest.Workflows.Platform;

/// <summary>
/// Runs a stock Crest.Workflows.Platform activity (Email, Users, Contents, Notifications, ...)
/// inside an engine activity. Everything a stock activity needs is public: it is
/// instantiated from the stock activity library (which every upstream module fills through
/// <c>AddActivity</c>), given the node's properties as its JSON, and handed a stock
/// execution context built from the engine's workflow input. The upstream module code is
/// untouched; only the engine underneath changed.
/// </summary>
public sealed class StockActivityRunner(IActivityLibrary activityLibrary, IServiceProvider serviceProvider)
{
    /// <summary>
    /// Stock activities that only mean something inside the stock engine: control flow,
    /// workflow state, correlation, signals, timers, the stock HTTP request pipeline and the
    /// forms tasks that read it. The engine has its own (Flowchart, If, ForEach, Fork/Join,
    /// variables, Correlate, Event, Timer, HttpEndpoint, ...); run through the adapter one
    /// would silently do nothing, so they are refused and never listed.
    /// </summary>
    public static readonly IReadOnlySet<string> EngineNative = new HashSet<string>(StringComparer.Ordinal)
    {
        "ForkTask", "JoinTask", "IfElseTask", "ForEachTask", "ForLoopTask", "WhileLoopTask",
        "SetPropertyTask", "SetOutputTask", "ScriptTask", "LiquidTask", "CorrelateTask", "CommitTransactionTask",
        "SignalEvent", "TimerEvent", "UserTaskEvent", "WorkflowFaultEvent",
        "HttpRequestEvent", "HttpRequestFilterEvent", "HttpResponseTask", "HttpRedirectTask",
        "HttpRedirectToFormLocationTask", "ValidateFormTask", "ValidateFormFieldTask", "BindModelStateTask",
        "AddModelValidationErrorTask", "ValidateAntiforgeryTokenTask", "ValidateReCaptchaTask",
    };

    /// <summary>
    /// Stock tasks whose effect leaves the database - mail, SMS, notifications, HTTP. An
    /// <see cref="PlatformTask"/> runs them two-phase (after its unit commits) and the
    /// atomicity analyzer treats them as boundaries.
    /// </summary>
    public static readonly IReadOnlySet<string> External = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "EmailTask", "SmsTask", "NotifyUserTask", "NotifyContentOwnerTask", "HttpRequestTask", "HttpRedirectTask", "HttpResponseTask",
    };

    public StockActivity Instantiate(string activityName, string? propertiesJson)
    {
        if (EngineNative.Contains(activityName))
        {
            throw new InvalidOperationException($"'{activityName}' is stock-engine control flow or state; use the engine's own activity instead.");
        }

        var activity = activityLibrary.InstantiateActivity(activityName)
            ?? throw new InvalidOperationException($"'{activityName}' is not a registered Crest workflow activity.");
        activity.Properties = string.IsNullOrWhiteSpace(propertiesJson) ? [] : JsonNode.Parse(propertiesJson) as JsonObject ?? [];
        return activity;
    }

    /// <summary>A stock execution context over the engine's input: one activity, no transitions.</summary>
    public (StockWorkflowExecutionContext Workflow, ActivityContext Activity) CreateContexts(
        StockActivity activity, string activityId, string workflowInstanceId, string definitionId, string? correlationId,
        IDictionary<string, object> input, StockWorkflowStatus status)
    {
        var record = new ActivityRecord { ActivityId = activityId, Name = activity.Name, Properties = activity.Properties, IsStart = status == StockWorkflowStatus.Starting };
        var type = new WorkflowType { WorkflowTypeId = definitionId, Name = definitionId, IsEnabled = true, Activities = [record] };
        var workflow = new Workflow { WorkflowId = workflowInstanceId, WorkflowTypeId = definitionId, CorrelationId = correlationId, Status = status, State = [] };
        var activityContext = new ActivityContext { ActivityRecord = record, Activity = activity };
        var workflowContext = new StockWorkflowExecutionContext(type, workflow, ToStockInput(input), new Dictionary<string, object>(), new Dictionary<string, object>(), [], null, [activityContext]);
        return (workflowContext, activityContext);
    }

    /// <summary>
    /// The engine persists workflow input as JSON, so values come back as JSON nodes or
    /// elements; stock activities expect plain dictionaries and scalars (a
    /// <c>ContentEventContext</c> arrives as a dictionary, which the stock base class maps).
    /// </summary>
    public static IDictionary<string, object> ToStockInput(IDictionary<string, object> input)
    {
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in input)
        {
            var plain = ToPlain(value);
            if (plain is not null)
            {
                result[key] = plain;
            }
        }

        return result;
    }

    public static object? ToPlain(object? value) => value switch
    {
        null => null,
        JsonElement element => ToPlain(element),
        JsonObject node => node.ToDictionary(p => p.Key, p => ToPlain(p.Value)!),
        JsonArray array => array.Select(ToPlain).ToList(),
        JsonValue jsonValue => ToPlain(jsonValue.GetValue<JsonElement>()),
        IDictionary<string, object> dictionary => dictionary.ToDictionary(p => p.Key, p => ToPlain(p.Value)!),
        _ => value,
    };

    private static object? ToPlain(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => ToPlain(p.Value)!),
        JsonValueKind.Array => element.EnumerateArray().Select(ToPlain).ToList(),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null,
    };
}
