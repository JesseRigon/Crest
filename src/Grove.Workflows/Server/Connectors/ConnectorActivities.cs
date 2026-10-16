using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grove.Access;
using Grove.Data.Connectors;
using Grove.Workflows.Activities.Flowchart.Attributes;
using Grove.Workflows.Attributes;
using Grove.Workflows.Expressions.Models;
using Grove.Workflows.Extensions;
using Grove.Workflows.Models;
using Grove.Workflows.Runtime;
using Grove.Workflows.Runtime.Stimuli;
using Grove.Workflows.Scheduling;
using Grove.Workflows.Scheduling.Bookmarks;
using Grove.Workflows.UIHints;
using Grove.Workflows.UIHints.Dropdown;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YesSql;

namespace Grove.Workflows.Connectors;

/// <summary>
/// What connector activities share: they run a connection's operation, or a raw request under
/// its base URL, through the connector engine (docs/connectors.md). They run as engine
/// <em>background activities</em> (docs/workflows.md › Posting on workflows): the burst
/// bookmarks the node and commits; the call is made afterwards in a unit of its own as the
/// instance's caller, with the job's idempotency key; the flow resumes with the result. The
/// engine owns retries (the connection's policy), so the node has no resilience strategy of
/// its own and attempts never multiply; the attempts the engine made are an output. When the
/// engine answers "retry after T", the node waits on a timer and runs the call again at T.
/// </summary>
public abstract class ConnectorActivityBase : Activity, IUnitBoundary
{
    /// <summary>Retry-after answers further away than this end the node on Failed instead of waiting.</summary>
    private static readonly TimeSpan LongestWait = TimeSpan.FromHours(24);

    [Input(DisplayName = "Connection", Description = "The connection to call (Connections).", UIHint = InputUIHints.DropDown, UIHandler = typeof(ConnectionOptionsProvider))]
    public Input<string> Connection { get; set; } = null!;

    [Input(DisplayName = "Operation", Description = "The connection's operation to run (its id). Empty: a raw request with the method and path below.", UIHint = InputUIHints.SingleLine)]
    public Input<string?> Operation { get; set; } = null!;

    [Input(DisplayName = "Arguments (JSON)", Description = "The operation's arguments as a JSON object: path, query and header parameters by name, the body under \"body\" (or the remaining arguments).", UIHint = InputUIHints.MultiLine)]
    public Input<string?> ArgumentsJson { get; set; } = null!;

    [Input(DisplayName = "Path", Description = "Raw request: relative to the connection's base URL; may carry a query string.", UIHint = InputUIHints.SingleLine)]
    public Input<string?> Path { get; set; } = null!;

    [Input(DisplayName = "Headers (JSON)", Description = "Raw request: extra request headers as a JSON object. The connection's auth is applied after them.", UIHint = InputUIHints.MultiLine)]
    public Input<string?> HeadersJson { get; set; } = null!;

    [Output(Description = "The status of the last attempt (0 when nothing was received).")]
    public Output<int> StatusCode { get; set; } = null!;

    [Output(Description = "The response body as text.")]
    public Output<string?> ResponseBody { get; set; } = null!;

    [Output(Description = "The response parsed (JSON, a SOAP body, SQL rows), when the protocol could parse it.")]
    public Output<object?> Response { get; set; } = null!;

    [Output(Description = "Why the activity ended on Failed, when it did.")]
    public Output<string?> Failure { get; set; } = null!;

    [Output(Description = "The attempts the connector engine made: number, status, failure kind, error, wait before.")]
    public Output<object?> Attempts { get; set; } = null!;

    /// <summary>
    /// Runs the call. Null when the engine answered "retry after T" and the node now waits on
    /// a timer (the activity is not complete).
    /// </summary>
    protected async ValueTask<ConnectorResult?> SendAsync(ActivityExecutionContext context, string method, string? body, string? contentType)
    {
        var connectionKey = Connection.GetOrDefault(context);
        var caller = context.GetRequiredService<ICallerContextAccessor>().Current;
        if (caller is null)
        {
            return Refused("No caller: connector calls run as the flow's caller.");
        }

        JsonObject? arguments = null;
        var argumentsJson = ArgumentsJson.GetOrDefault(context);
        if (!string.IsNullOrWhiteSpace(argumentsJson))
        {
            try
            {
                arguments = JsonNode.Parse(argumentsJson) as JsonObject ?? throw new JsonException();
            }
            catch (JsonException)
            {
                return Refused("Arguments must be a JSON object.");
            }
        }

        var operation = Operation.GetOrDefault(context);
        ConnectorRequest? request = null;
        if (string.IsNullOrWhiteSpace(operation))
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var headersJson = HeadersJson.GetOrDefault(context);
            if (!string.IsNullOrWhiteSpace(headersJson))
            {
                try
                {
                    foreach (var (name, value) in JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson) ?? [])
                    {
                        headers[name] = value;
                    }
                }
                catch (JsonException)
                {
                    return Refused("Headers must be a JSON object of strings.");
                }
            }

            request = new ConnectorRequest(method, Path.GetOrDefault(context), body, contentType, headers);
        }

        var job = context.GetService<Units.BackgroundJobContext>()?.Current;
        var result = await context.GetRequiredService<IConnectorEngine>().ExecuteAsync(new ConnectorCall
        {
            ConnectionKey = connectionKey,
            Caller = caller,
            OperationId = string.IsNullOrWhiteSpace(operation) ? null : operation.Trim(),
            Arguments = arguments,
            Request = request,
            // The scheduled run's key, stable across retries of this run: an unsafe call may be retried with it.
            IdempotencyKey = job?.IdempotencyKey,
        }, context.CancellationToken);

        StatusCode.Set(context, result.StatusCode);
        ResponseBody.Set(context, result.Body);
        Response.Set(context, result.Value is null ? null : Services.JsonPlain.ToPlain(JsonSerializer.SerializeToElement(result.Value)));
        Attempts.Set(context, result.Attempts.Select(a => new Dictionary<string, object?>
        {
            ["Number"] = a.Number,
            ["StatusCode"] = a.StatusCode,
            ["Failure"] = a.Failure.ToString(),
            ["Error"] = a.Error,
            ["WaitBeforeMs"] = a.WaitBefore?.TotalMilliseconds,
            ["StartedUtc"] = a.StartedUtc,
        }).ToList());

        if (result.Outcome == ConnectorOutcome.RetryAfter && result.RetryAt is { } retryAt && retryAt - DateTimeOffset.UtcNow <= LongestWait)
        {
            context.GetRequiredService<ILogger<ConnectorActivityBase>>().LogInformation("{Activity} on {Connection}: the service asked to retry at {RetryAt}; the node waits.", GetType().Name, connectionKey, retryAt);
            context.CreateBookmark(new CreateBookmarkArgs
            {
                BookmarkName = SchedulingStimulusNames.Delay,
                Stimulus = new DelayPayload(retryAt),
                Callback = RetryDueAsync,
                AutoComplete = false,
            });
            return null;
        }

        return result;
    }

    /// <summary>
    /// The timer of a "retry after" fired: schedule the call again as a background job, the
    /// way the engine schedules a background activity, so the call never runs inside a burst.
    /// </summary>
    private async ValueTask RetryDueAsync(ActivityExecutionContext context)
    {
        var scheduler = context.GetRequiredService<IBackgroundActivityScheduler>();
        var bookmarkId = context.GetRequiredService<IIdentityGenerator>().GenerateId();
        var jobId = await scheduler.CreateAsync(new ScheduledBackgroundActivity(context.WorkflowExecutionContext.Id, context.NodeId, bookmarkId), context.CancellationToken);
        context.CreateBookmark(new CreateBookmarkArgs
        {
            BookmarkId = bookmarkId,
            BookmarkName = "BackgroundActivity",
            Stimulus = new BackgroundActivityStimulus { JobId = jobId },
            AutoComplete = false,
        });
        var cancellationToken = context.CancellationToken;
        context.DeferTask(async () => await scheduler.ScheduleAsync(jobId, cancellationToken));
    }

    protected async ValueTask FailAsync(ActivityExecutionContext context, string reason)
    {
        Failure.Set(context, reason);
        context.GetRequiredService<ILogger<ConnectorActivityBase>>().LogWarning("{Activity} failed: {Reason}", GetType().Name, reason);
        await context.CompleteActivityWithOutcomesAsync("Failed");
    }

    private static ConnectorResult Refused(string error) => new() { Outcome = ConnectorOutcome.Refused, Failure = FailureKind.Refused, Error = error };
}

/// <summary>
/// Runs a connection's operation (or a raw request) through the connector engine: the
/// credential, retries, rate limit and caching are the connection's, never the definition's.
/// The call happens after this flow's unit commits and the flow resumes with the result. Done
/// on success, Failed otherwise with the reason in <c>Failure</c>.
/// </summary>
[Activity("Grove.Workflows", "Connectors", "Runs a connection's operation (auth, retries and rate limits from the connection), after this flow's transaction commits.", DisplayName = "Call connector", Kind = ActivityKind.Task, RunAsynchronously = true)]
[FlowNode("Done", "Failed")]
[RequiresPermission(ConnectorsPermissionNames.UseConnections)]
public class CallConnector : ConnectorActivityBase
{
    [Input(DisplayName = "Method", Description = "Raw request: the HTTP method.", DefaultValue = "POST", Options = new[] { "GET", "POST", "PUT", "PATCH", "DELETE" }, UIHint = InputUIHints.DropDown)]
    public Input<string> Method { get; set; } = null!;

    [Input(DisplayName = "Body", Description = "Raw request: the body (usually JSON; an expression can build it from the trigger payload).", UIHint = InputUIHints.MultiLine)]
    public Input<string?> Body { get; set; } = null!;

    [Input(DisplayName = "Content type", DefaultValue = "application/json", UIHint = InputUIHints.SingleLine)]
    public Input<string?> ContentType { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var method = Method.GetOrDefault(context);
        var result = await SendAsync(context, string.IsNullOrWhiteSpace(method) ? "POST" : method, Body.GetOrDefault(context), ContentType.GetOrDefault(context));
        if (result is null)
        {
            return;
        }

        if (!result.Succeeded)
        {
            await FailAsync(context, result.Error ?? result.Outcome.ToString());
            return;
        }

        await context.CompleteActivityWithOutcomesAsync("Done");
    }
}

/// <summary>
/// Reads through a connection (a safe operation, or a raw GET) and tells whether the result
/// changed since this node last looked (a hash per definition and node, kept in the tenant's
/// database). Put it after a Cron or Timer trigger to poll a service that has no webhooks. The
/// first poll is Changed.
/// </summary>
[Activity("Grove.Workflows", "Connectors", "Reads through a connection and continues on Changed when the result differs from the last poll.", DisplayName = "Poll connector", Kind = ActivityKind.Task, RunAsynchronously = true)]
[FlowNode("Changed", "Unchanged", "Failed")]
[RequiresPermission(ConnectorsPermissionNames.UseConnections)]
public class PollConnector : ConnectorActivityBase
{
    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var result = await SendAsync(context, "GET", null, null);
        if (result is null)
        {
            return;
        }

        if (!result.Succeeded)
        {
            await FailAsync(context, result.Error ?? result.Outcome.ToString());
            return;
        }

        var stateKey = $"{context.WorkflowExecutionContext.Workflow.Identity.DefinitionId}:{context.Activity.Id}:{Connection.GetOrDefault(context)}:{Operation.GetOrDefault(context)}:{Path.GetOrDefault(context)}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(result.Body ?? result.Value?.ToJsonString() ?? string.Empty)));
        var session = context.GetRequiredService<ISession>();
        var state = await session.Query<ConnectorPollStateDocument>().FirstOrDefaultAsync() ?? new ConnectorPollStateDocument();
        var changed = !state.Hashes.TryGetValue(stateKey, out var previous) || previous != hash;
        if (changed)
        {
            state.Hashes[stateKey] = hash;
            await session.SaveAsync(state);
        }

        await context.CompleteActivityWithOutcomesAsync(changed ? "Changed" : "Unchanged");
    }
}

/// <summary>The last response hash of every Poll connector node in the tenant.</summary>
public sealed class ConnectorPollStateDocument
{
    public long Id { get; set; }
    public Dictionary<string, string> Hashes { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>The stimulus an inbound webhook matches on.</summary>
public sealed record WebhookStimulus(string Connection, string Hook);

/// <summary>
/// Fires when an external service posts to the tenant's webhook URL
/// (<c>api/grove/workflows/webhooks/{connection}/{hook}</c>) with a signature the connection's
/// verifier accepts. The result is the payload: <c>Body</c> (parsed JSON or text),
/// <c>Query</c>, <c>ContentType</c>. The run acts as the connection's application caller.
/// </summary>
[Activity("Grove.Workflows", "Connectors", "Fires when an external service posts a signed request to this tenant's webhook URL.", DisplayName = "Webhook received")]
public class WebhookReceived : Trigger<IDictionary<string, object>>
{
    [Input(DisplayName = "Connection", Description = "An inbound (signed webhook) connection; the connector engine verifies the signature.", UIHint = InputUIHints.DropDown, UIHandler = typeof(ConnectionOptionsProvider))]
    public Input<string> Connection { get; set; } = null!;

    [Input(DisplayName = "Hook", Description = "The last URL segment, e.g. invoice-paid.", UIHint = InputUIHints.SingleLine)]
    public Input<string> Hook { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        if (context.IsTriggerOfWorkflow())
            await ExecuteInternalAsync(context);
        else
            context.CreateBookmarks(GetStimuli(context.ExpressionExecutionContext), ExecuteInternalAsync, false);
    }

    protected override ValueTask<IEnumerable<object>> GetTriggerPayloadsAsync(TriggerIndexingContext context) => new(GetStimuli(context.ExpressionExecutionContext));

    private IEnumerable<object> GetStimuli(ExpressionExecutionContext context)
    {
        var connection = Connection.GetOrDefault(context);
        var hook = Hook.GetOrDefault(context);
        return string.IsNullOrWhiteSpace(connection) || string.IsNullOrWhiteSpace(hook) ? [] : [Stimulus(connection, hook)];
    }

    public static WebhookStimulus Stimulus(string connection, string hook) => new(connection.Trim().ToLowerInvariant(), hook.Trim().ToLowerInvariant());

    private async ValueTask ExecuteInternalAsync(ActivityExecutionContext context)
    {
        context.SetResult(context.FindWorkflowInput<IDictionary<string, object>>(WorkflowsConstants.InputKeys.Payload) ?? new Dictionary<string, object>());
        await context.CompleteActivityAsync();
    }
}

/// <summary>The tenant's connections, for connection inputs.</summary>
public sealed class ConnectionOptionsProvider(IConnectionRegistry connections) : DropDownOptionsProviderBase
{
    protected override async ValueTask<ICollection<SelectListItem>> GetItemsAsync(PropertyInfo propertyInfo, object? context, CancellationToken cancellationToken) =>
        (await connections.ListAsync(cancellationToken)).Select(c => new SelectListItem($"{c.DisplayName} ({c.Key})", c.Key)).ToList();
}

/// <summary>
/// Every operation of every connection as a palette entry: Call connector with the connection
/// and operation preset, keyed <c>connector.{connection}.{operationId}</c>. A flow author picks
/// "Accountant API › createInvoice" and binds the arguments.
/// </summary>
public sealed class ConnectionOperationActivityProvider(IConnectionRegistry connections) : IWorkflowActivityProvider
{
    public IEnumerable<WorkflowActivityDescriptor> Activities
    {
        get
        {
            var position = 2000;
            foreach (var entry in connections.ListOperationsAsync().GetAwaiter().GetResult())
            {
                yield return new WorkflowActivityDescriptor(
                    $"connector.{entry.ConnectionKey}.{entry.Operation.Id}",
                    $"{entry.ConnectionDisplayName} › {entry.Operation.DisplayName ?? entry.Operation.Id}",
                    WorkflowsConstants.Objects.Workflow,
                    "Grove.Workflows.CallConnector",
                    entry.Operation.Description ?? $"{entry.Operation.Method} {entry.Operation.Path}".Trim(),
                    position++,
                    Category: entry.ConnectionDisplayName,
                    Inputs: new Dictionary<string, string>
                    {
                        ["connection"] = entry.ConnectionKey,
                        ["operation"] = entry.Operation.Id,
                    });
            }
        }
    }
}
