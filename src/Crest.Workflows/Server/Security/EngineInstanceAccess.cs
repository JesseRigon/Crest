using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Crest.Access;
using Crest.Workflows.Helpers;
using Crest.Workflows.Management;
using Crest.Workflows.Management.Filters;
using Crest.Workflows.Runtime;
using Crest.Workflows.Runtime.Activities;
using Crest.Workflows.Runtime.Filters;
using Crest.Workflows.Runtime.Stimuli;
using Crest.Workflows.SasTokens.Contracts;
using Microsoft.AspNetCore.Http;

namespace Crest.Workflows.Security;

/// <summary>
/// The per-definition narrowing of the engine API's instance operations (docs/workflows.md
/// › Ownership and permissions, the View list; docs/access.md, the instance-read defect):
/// what definitions a request touches and what it asks of each, decided with the definition
/// as the resource. An instance read (get, journal, variables, execution state, export,
/// activity executions) takes View against the definition's View list; cancelling, deleting
/// or changing an instance, a bulk action, a bookmark resume, a task completion and an event
/// trigger take Run against its Run list. The instance list is narrowed to the definitions
/// whose View list admits the caller. Everything is resolved the way the engine resolves it
/// (instance ids to their definition, a task or an event to the bookmarks and triggers its
/// stimulus hashes to), so what the engine would act on is what is decided.
/// </summary>
public sealed class EngineInstanceAccess(
    IAccessDecision decision,
    IWorkflowDefinitionAccessReader access,
    IWorkflowInstanceStore instances,
    IWorkflowDefinitionStore definitions,
    IActivityExecutionStore activityExecutions,
    IBookmarkStore bookmarks,
    ITriggerStore triggers,
    IStimulusHasher hasher,
    ITokenService tokens)
{
    private static readonly string RunTaskTypeName = ActivityTypeNameHelper.GenerateTypeName<RunTask>();
    private static readonly string EventTypeName = ActivityTypeNameHelper.GenerateTypeName<Event>();

    /// <summary>Null when the request may proceed (the instance list narrowed in place), else why not.</summary>
    public async Task<string?> CheckAsync(HttpContext context, PathString remaining, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var segments = remaining.Value?.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];
        var method = context.Request.Method;
        var isGet = HttpMethods.IsGet(method);

        if (segments is ["workflow-instances"])
        {
            return await NarrowListAsync(context, caller, cancellationToken);
        }

        if (segments is ["workflow-instances", var id, ..] && !string.Equals(id, "bulk", StringComparison.OrdinalIgnoreCase))
        {
            // The journal's filtered list posts its filter: a read.
            var read = isGet || (segments.Length >= 3 && string.Equals(segments[2], "journal", StringComparison.OrdinalIgnoreCase));
            return await RequireInstancesAsync(caller, [Uri.UnescapeDataString(id)], read ? WorkflowsConstants.Permissions.View : WorkflowsConstants.Permissions.Run, cancellationToken);
        }

        if (segments is ["cancel", "workflow-instances", var cancelled])
        {
            return await RequireInstancesAsync(caller, [Uri.UnescapeDataString(cancelled)], WorkflowsConstants.Permissions.Run, cancellationToken);
        }

        if (segments is ["bulk-actions", var action, "workflow-instances", ..])
        {
            return await CheckBulkAsync(context, action, caller, cancellationToken);
        }

        if (segments is ["activity-executions", var what, ..])
        {
            var instanceId = what.ToLowerInvariant() switch
            {
                "list" or "count" => Query(context, "workflowInstanceId"),
                "report" => (await ReadBodyAsync(context))?["workflowInstanceId"]?.GetValue<string>(),
                _ => (await activityExecutions.FindAsync(new ActivityExecutionRecordFilter { Id = Uri.UnescapeDataString(what) }, cancellationToken))?.WorkflowInstanceId,
            };
            return instanceId is null ? null : await RequireInstancesAsync(caller, [instanceId], WorkflowsConstants.Permissions.View, cancellationToken);
        }

        if (segments is ["activity-execution-summaries", ..])
        {
            var instanceId = Query(context, "workflowInstanceId");
            return instanceId is null ? null : await RequireInstancesAsync(caller, [instanceId], WorkflowsConstants.Permissions.View, cancellationToken);
        }

        if (segments is ["bookmarks", "resume"])
        {
            // The SAS token names the instance; an unreadable token is the engine's to refuse.
            var token = Query(context, "t");
            return token is not null && tokens.TryDecryptToken<BookmarkTokenPayload>(token, out var payload)
                ? await RequireInstancesAsync(caller, [payload.WorkflowInstanceId], WorkflowsConstants.Permissions.Run, cancellationToken)
                : null;
        }

        if (segments is ["tasks", var taskId, "complete"])
        {
            var hash = hasher.Hash(RunTaskTypeName, new RunTaskStimulus(Uri.UnescapeDataString(taskId), null!));
            var waiting = await bookmarks.FindManyAsync(new BookmarkFilter { Hash = hash }, cancellationToken);
            return await RequireInstancesAsync(caller, waiting.Select(bookmark => bookmark.WorkflowInstanceId).ToList(), WorkflowsConstants.Permissions.Run, cancellationToken);
        }

        if (segments is ["events", var eventName, "trigger"])
        {
            return await CheckEventAsync(context, Uri.UnescapeDataString(eventName), caller, cancellationToken);
        }

        return null;
    }

    private async Task<string?> CheckBulkAsync(HttpContext context, string action, CallerContext caller, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync(context);
        var ids = Strings(body, "ids").Concat(context.Request.Query["ids"].Where(id => !string.IsNullOrEmpty(id))!).Distinct(StringComparer.Ordinal).ToList();
        if (Query(context, "id") is { } single)
        {
            ids.Add(single);
        }

        switch (action.ToLowerInvariant())
        {
            case "export":
                return await RequireInstancesAsync(caller, ids, WorkflowsConstants.Permissions.View, cancellationToken);

            case "cancel":
            case "delete":
                if (ids.Count > 0)
                {
                    return await RequireInstancesAsync(caller, ids, WorkflowsConstants.Permissions.Run, cancellationToken);
                }

                var definitionIds = Strings(body, "workflowDefinitionIds").Concat(Strings(body, "definitionIds")).ToList();
                foreach (var key in new[] { "workflowDefinitionId", "definitionId" })
                {
                    if (body?[key]?.GetValue<string>() is { Length: > 0 } definitionId)
                    {
                        definitionIds.Add(definitionId);
                    }
                }

                if (body?["definitionVersionId"]?.GetValue<string>() is { Length: > 0 } versionId
                    && await definitions.FindAsync(new WorkflowDefinitionFilter { Id = versionId }, cancellationToken) is { } version)
                {
                    definitionIds.Add(version.DefinitionId);
                }

                // Nothing named: the action reaches every instance, so every definition's
                // Run list must admit the caller.
                return definitionIds.Count > 0
                    ? await RequireDefinitionsAsync(caller, definitionIds, WorkflowsConstants.Permissions.Run, cancellationToken)
                    : await RequireAllDefinitionsAsync(caller, WorkflowsConstants.Permissions.Run, cancellationToken);

            default:
                // An import carries its instances in files; the dispatcher stamps the actor.
                return null;
        }
    }

    private async Task<string?> CheckEventAsync(HttpContext context, string eventName, CallerContext caller, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync(context);
        if (body?["workflowInstanceId"]?.GetValue<string>() is { Length: > 0 } instanceId)
        {
            return await RequireInstancesAsync(caller, [instanceId], WorkflowsConstants.Permissions.Run, cancellationToken);
        }

        // What the engine would start or resume: the triggers and bookmarks the event hashes to.
        var hash = hasher.Hash(EventTypeName, new EventStimulus(eventName));
        var started = (await triggers.FindManyAsync(new TriggerFilter { Hash = hash }, cancellationToken)).Select(trigger => trigger.WorkflowDefinitionId).ToList();
        var resumed = (await bookmarks.FindManyAsync(new BookmarkFilter { Hash = hash }, cancellationToken)).Select(bookmark => bookmark.WorkflowInstanceId).ToList();

        return await RequireDefinitionsAsync(caller, started, WorkflowsConstants.Permissions.Run, cancellationToken)
            ?? await RequireInstancesAsync(caller, resumed, WorkflowsConstants.Permissions.Run, cancellationToken);
    }

    /// <summary>
    /// The instance list, narrowed to the definitions whose View list admits the caller: the
    /// request's definition filter is intersected with them (a filter left with nothing is
    /// refused), or added when the request had none and some definition excludes the caller.
    /// </summary>
    private async Task<string?> NarrowListAsync(HttpContext context, CallerContext caller, CancellationToken cancellationToken)
    {
        var all = await access.ListAsync(cancellationToken);
        var excluded = all.Where(resource => !resource.Admits(caller, WorkflowsConstants.Permissions.View)).Select(resource => resource.DefinitionId).ToHashSet(StringComparer.Ordinal);
        if (excluded.Count == 0)
        {
            return null;
        }

        var body = HttpMethods.IsGet(context.Request.Method) ? null : await ReadBodyAsync(context);
        var requested = Strings(body, "definitionIds")
            .Concat(context.Request.Query["definitionIds"].Where(id => !string.IsNullOrEmpty(id))!)
            .ToList();
        foreach (var single in new[] { body?["definitionId"]?.GetValue<string>(), Query(context, "definitionId") })
        {
            if (!string.IsNullOrEmpty(single))
            {
                requested.Add(single);
            }
        }

        var allowed = (requested.Count > 0 ? requested : all.Select(resource => resource.DefinitionId))
            .Where(id => !excluded.Contains(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (allowed.Count == 0)
        {
            return "The workflows asked for limit who may read their instances.";
        }

        if (body is not null)
        {
            body.Remove("definitionId");
            body["definitionIds"] = new JsonArray(allowed.Select(id => (JsonNode?)id).ToArray());
            ReplaceBody(context, body);
        }
        else
        {
            var query = context.Request.Query
                .Where(pair => !string.Equals(pair.Key, "definitionId", StringComparison.OrdinalIgnoreCase) && !string.Equals(pair.Key, "definitionIds", StringComparison.OrdinalIgnoreCase))
                .SelectMany(pair => pair.Value.Select(value => new KeyValuePair<string, string?>(pair.Key, value)))
                .Concat(allowed.Select(id => new KeyValuePair<string, string?>("definitionIds", id)));
            context.Request.QueryString = QueryString.Create(query);
        }

        return null;
    }

    private async Task<string?> RequireInstancesAsync(CallerContext caller, IReadOnlyList<string> instanceIds, string permission, CancellationToken cancellationToken)
    {
        if (instanceIds.Count == 0)
        {
            return null;
        }

        // An instance that does not exist is the engine's 404; it names no definition here.
        var summaries = await instances.SummarizeManyAsync(new WorkflowInstanceFilter { Ids = instanceIds.ToList() }, cancellationToken);
        return await RequireDefinitionsAsync(caller, summaries.Select(summary => summary.DefinitionId).ToList(), permission, cancellationToken);
    }

    private async Task<string?> RequireDefinitionsAsync(CallerContext caller, IReadOnlyList<string> definitionIds, string permission, CancellationToken cancellationToken)
    {
        foreach (var definitionId in definitionIds.Distinct(StringComparer.Ordinal))
        {
            var resource = await access.GetAsync(definitionId) ?? WorkflowDefinitionAccessResource.Open(definitionId);
            if (!await MayAsync(caller, resource, permission, cancellationToken))
            {
                return Refusal(resource, permission);
            }
        }

        return null;
    }

    private async Task<string?> RequireAllDefinitionsAsync(CallerContext caller, string permission, CancellationToken cancellationToken)
    {
        foreach (var resource in await access.ListAsync(cancellationToken))
        {
            if (!await MayAsync(caller, resource, permission, cancellationToken))
            {
                return Refusal(resource, permission);
            }
        }

        return null;
    }

    /// <summary>The decision for the permission with the definition as the resource (the ceiling reads its lists), and the lists as the veto beside it.</summary>
    public async Task<bool> MayAsync(CallerContext caller, WorkflowDefinitionAccessResource resource, string permission, CancellationToken cancellationToken = default) =>
        (await decision.DecideAsync(caller, permission, resource, cancellationToken)).IsAllowed && resource.Admits(caller, permission);

    private static string Refusal(WorkflowDefinitionAccessResource resource, string permission) =>
        permission == WorkflowsConstants.Permissions.View
            ? $"Workflow '{resource.DefinitionId}' limits who may read its instances."
            : $"You may not run workflow '{resource.DefinitionId}'.";

    private static string? Query(HttpContext context, string name) =>
        context.Request.Query.TryGetValue(name, out var values) && values.ToString() is { Length: > 0 } value ? value : null;

    private static IEnumerable<string> Strings(JsonObject? body, string key) =>
        body?[key] is JsonArray array ? array.Select(node => node?.GetValue<string>()).Where(id => !string.IsNullOrEmpty(id))! : [];

    /// <summary>The JSON body, re-readable afterwards; null for no body or no JSON object.</summary>
    private static async Task<JsonObject?> ReadBodyAsync(HttpContext context)
    {
        var request = context.Request;
        if (HttpMethods.IsGet(request.Method) || request.ContentLength == 0 || request.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) != true)
        {
            return null;
        }

        request.EnableBuffering();
        request.Body.Position = 0;
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var text = await reader.ReadToEndAsync();
        request.Body.Position = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(text, new JsonNodeOptions { PropertyNameCaseInsensitive = true }) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void ReplaceBody(HttpContext context, JsonObject body)
    {
        var bytes = Encoding.UTF8.GetBytes(body.ToJsonString());
        context.Request.Body = new MemoryStream(bytes);
        context.Request.ContentLength = bytes.Length;
    }
}
