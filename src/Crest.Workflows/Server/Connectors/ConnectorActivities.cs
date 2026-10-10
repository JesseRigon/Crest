using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Crest.Workflows.Activities.Flowchart.Attributes;
using Crest.Workflows.Attributes;
using Crest.Workflows.Expressions.Models;
using Crest.Workflows.Extensions;
using Crest.Workflows.Models;
using Crest.Workflows.Platform;
using Crest.Workflows.Resilience;
using Crest.Workflows.Resilience.Models;
using Crest.Workflows.UIHints;
using Crest.Workflows.UIHints.Dropdown;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YesSql;

namespace Crest.Workflows.Connectors;

/// <summary>
/// What connector activities share: the connection, the request, and the response outputs.
/// Connector activities run as engine <em>background activities</em> (`RunAsynchronously`,
/// docs/workflows.md › Posting on workflows): the burst bookmarks the node and commits; the
/// call is made afterwards in a unit of its own (<see cref="Units.DurableBackgroundActivityScheduler"/>)
/// with the connection's auth, retries and rate limit and the job's <c>Idempotency-Key</c>;
/// the flow resumes with the response as the next burst. A unit that fails never makes the
/// call.
/// </summary>
public abstract class ConnectorActivityBase : Activity, Units.IUnitBoundary, IResilientActivity
{
    public const string IdempotencyHeader = "Idempotency-Key";
    private const string ResilienceStrategyProperty = "resilienceStrategy";

    [Input(DisplayName = "Connection", Description = "The tenant connection to call (Workflows > Connections).", UIHint = InputUIHints.DropDown, UIHandler = typeof(ConnectionOptionsProvider))]
    public Input<string> Connection { get; set; } = null!;

    [Input(DisplayName = "Path", Description = "Relative to the connection's base URL; may carry a query string. Expressions can map the trigger payload in.", UIHint = InputUIHints.SingleLine)]
    public Input<string?> Path { get; set; } = null!;

    [Input(DisplayName = "Headers (JSON)", Description = "Optional extra request headers as a JSON object. The connection's auth is applied after them.", UIHint = InputUIHints.MultiLine)]
    public Input<string?> HeadersJson { get; set; } = null!;

    [Output(Description = "The HTTP status of the last attempt (0 when nothing was received).")]
    public Output<int> StatusCode { get; set; } = null!;

    [Output(Description = "The response body as text.")]
    public Output<string?> ResponseBody { get; set; } = null!;

    [Output(Description = "The response body parsed as JSON, when it is JSON.")]
    public Output<object?> Response { get; set; } = null!;

    [Output(Description = "Why the activity ended on Failed, when it did.")]
    public Output<string?> Failure { get; set; } = null!;

    protected async ValueTask<ConnectorResponse> SendAsync(ActivityExecutionContext context, string method, string? body, string? contentType)
    {
        var connectionKey = Connection.GetOrDefault(context);
        var connection = await context.GetRequiredService<WorkflowConnectionService>().FindAsync(connectionKey);
        if (connection is null)
        {
            return ConnectorResponse.Failed($"No connection '{connectionKey}' in this tenant.");
        }

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
                return ConnectorResponse.Failed("Headers must be a JSON object of strings.");
            }
        }

        // The scheduled run's key, stable across retries of this run.
        if (context.GetService<Units.BackgroundJobContext>()?.Current is { } job)
        {
            headers.TryAdd(IdempotencyHeader, job.IdempotencyKey);
        }

        // Retries are the engine's resilience feature: the connection's policy unless the node picked another strategy.
        CustomProperties.TryAdd(ResilienceStrategyProperty, new ResilienceStrategyConfig { Mode = ResilienceStrategyConfigMode.Identifier, StrategyId = ConnectionResilienceStrategy.StrategyId });
        var request = new ConnectorRequest(method, Path.GetOrDefault(context), body, contentType, headers);
        var invoker = context.GetRequiredService<ConnectorInvoker>();
        var response = await context.GetRequiredService<IResilientActivityInvoker>().InvokeAsync(this, context, () => invoker.SendAsync(connection, request, context.CancellationToken), context.CancellationToken);
        StatusCode.Set(context, response.StatusCode);
        ResponseBody.Set(context, response.Body);
        Response.Set(context, TryParse(response.Body));
        return response;
    }

    /// <summary>What the journal shows per retry attempt.</summary>
    public IDictionary<string, string?> CollectRetryDetails(ActivityExecutionContext context, RetryAttempt attempt) => new Dictionary<string, string?>
    {
        ["Connection"] = Connection.GetOrDefault(context),
        ["Path"] = Path.GetOrDefault(context),
    };

    protected async ValueTask FailAsync(ActivityExecutionContext context, string reason)
    {
        Failure.Set(context, reason);
        context.GetRequiredService<ILogger<ConnectorActivityBase>>().LogWarning("{Activity} failed: {Reason}", GetType().Name, reason);
        await context.CompleteActivityWithOutcomesAsync("Failed");
    }

    private static object? TryParse(string? body)
    {
        if (string.IsNullOrWhiteSpace(body) || body.TrimStart()[0] is not ('{' or '['))
        {
            return null;
        }

        try
        {
            using var json = JsonDocument.Parse(body);
            return StockActivityRunner.ToPlain(json.RootElement.Clone());
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// Calls an external service through a tenant connection: auth from the connection
/// (sealed secret, never in the definition), retries and rate limit from the connection,
/// the body and path from expressions over the workflow's input. The call happens after
/// this flow's unit commits and the flow resumes with the response. Done on a 2xx, Failed
/// otherwise with the reason in <c>Failure</c>.
/// </summary>
[Activity("Crest.Workflows", "Connectors", "Calls an external API through a tenant connection (auth, retries and rate limits from the connection), after this flow's transaction commits.", DisplayName = "Call connector", Kind = ActivityKind.Task, RunAsynchronously = true)]
[FlowNode("Done", "Failed")]
[RequiresPermission(WorkflowsConstants.Permissions.UseConnections)]
public class CallConnector : ConnectorActivityBase
{
    [Input(DisplayName = "Method", DefaultValue = "POST", Options = new[] { "GET", "POST", "PUT", "PATCH", "DELETE" }, UIHint = InputUIHints.DropDown)]
    public Input<string> Method { get; set; } = null!;

    [Input(DisplayName = "Body", Description = "The request body (usually JSON; an expression can build it from the trigger payload).", UIHint = InputUIHints.MultiLine)]
    public Input<string?> Body { get; set; } = null!;

    [Input(DisplayName = "Content type", DefaultValue = "application/json", UIHint = InputUIHints.SingleLine)]
    public Input<string?> ContentType { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var method = Method.GetOrDefault(context);
        var response = await SendAsync(context, string.IsNullOrWhiteSpace(method) ? "POST" : method, Body.GetOrDefault(context), ContentType.GetOrDefault(context));
        if (!response.Succeeded)
        {
            await FailAsync(context, response.Error ?? "Failed.");
            return;
        }

        await context.CompleteActivityWithOutcomesAsync("Done");
    }
}

/// <summary>
/// GETs a connection's resource and tells whether it changed since this node last looked
/// (a hash per definition and node, kept in the tenant's database). Put it after a Cron or
/// Timer trigger to poll a service that has no webhooks. The first poll is Changed.
/// </summary>
[Activity("Crest.Workflows", "Connectors", "Fetches a resource through a tenant connection and continues on Changed when it differs from the last poll.", DisplayName = "Poll connector", Kind = ActivityKind.Task, RunAsynchronously = true)]
[FlowNode("Changed", "Unchanged", "Failed")]
[RequiresPermission(WorkflowsConstants.Permissions.UseConnections)]
public class PollConnector : ConnectorActivityBase
{
    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var response = await SendAsync(context, "GET", null, null);
        if (!response.Succeeded)
        {
            await FailAsync(context, response.Error ?? "Failed.");
            return;
        }

        var stateKey = $"{context.WorkflowExecutionContext.Workflow.Identity.DefinitionId}:{context.Activity.Id}:{Connection.GetOrDefault(context)}:{Path.GetOrDefault(context)}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(response.Body ?? string.Empty)));
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
/// (<c>api/crest/workflows/webhooks/{connection}/{hook}</c>) with a valid HMAC signature
/// made with the connection's secret. The result is the payload: <c>Body</c> (parsed JSON
/// or text), <c>Query</c>, <c>ContentType</c>. There is no acting user.
/// </summary>
[Activity("Crest.Workflows", "Connectors", "Fires when an external service posts a signed request to this tenant's webhook URL.", DisplayName = "Webhook received")]
public class WebhookReceived : Trigger<IDictionary<string, object>>
{
    [Input(DisplayName = "Connection", Description = "An hmac connection; its secret verifies the signature.", UIHint = InputUIHints.DropDown, UIHandler = typeof(ConnectionOptionsProvider))]
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
public sealed class ConnectionOptionsProvider(WorkflowConnectionService connections) : DropDownOptionsProviderBase
{
    protected override async ValueTask<ICollection<SelectListItem>> GetItemsAsync(PropertyInfo propertyInfo, object? context, CancellationToken cancellationToken) =>
        (await connections.ListAsync()).Select(c => new SelectListItem($"{c.DisplayName} ({c.Key})", c.Key)).ToList();
}
