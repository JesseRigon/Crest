using Crest.Workflows.Activities.Flowchart.Attributes;
using Crest.Workflows.Attributes;
using Crest.Workflows.Contexts;
using Crest.Workflows.Models;
using Crest.Workflows.UIHints;
using Crest.Workflows.Expressions.Models;
using Crest.Workflows.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Workflows.Registry;

/// <summary>The stimulus a Registry trigger matches on: the registered key.</summary>
public sealed record RegistryTriggerStimulus(string TriggerKey);

/// <summary>
/// Fires when a Crest registry raises a trigger (transaction.posted, party.role-created,
/// ...). The result is the payload the registry sent; the acting user rides along as the
/// workflow input <c>Actor</c>. Optional <see cref="RequiredPermission"/> ends the run on
/// Denied when that user lacks the Crest permission; optional <see cref="PayloadFilter"/>
/// ends it on Skipped when the payload does not match (docs/workflows.md, phase 5: "when a
/// transaction hits account X" is one node: trigger <c>transaction.account-posted</c>,
/// filter <c>AccountCode = 1200</c>).
/// </summary>
[Activity("Crest.Workflows", "Crest", "Fires when a Crest registry raises a trigger (transaction posted, party role created, ...).", DisplayName = "Registry trigger")]
[FlowNode("Done", "Denied", "Skipped")]
public class RegistryTrigger : Trigger<IDictionary<string, object>>
{
    [Input(DisplayName = "Trigger", Description = "The registered trigger key, e.g. transaction.posted.", UIHint = InputUIHints.DropDown, UIHandler = typeof(WorkflowTriggerOptionsProvider))]
    public Input<string> TriggerKey { get; set; } = null!;

    [Input(DisplayName = "Required permission", Description = "Optional. Crest permission the acting user must hold for the flow to proceed; otherwise the run ends on Denied.", UIHint = InputUIHints.DropDown, UIHandler = typeof(PermissionOptionsProvider))]
    public Input<string?> RequiredPermission { get; set; } = null!;

    [Input(DisplayName = "Payload filter", Description = "Optional. One 'Key = value' per line; every line must match a payload value (case-insensitive) or the run ends on Skipped. Example: Kind = invoice.", UIHint = InputUIHints.MultiLine)]
    public Input<string?> PayloadFilter { get; set; } = null!;

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
        var key = TriggerKey.GetOrDefault(context);
        return string.IsNullOrWhiteSpace(key) ? [] : [new RegistryTriggerStimulus(key.Trim().ToLowerInvariant())];
    }

    private async ValueTask ExecuteInternalAsync(ActivityExecutionContext context)
    {
        var requiredPermission = RequiredPermission.GetOrDefault(context);
        if (!string.IsNullOrWhiteSpace(requiredPermission))
        {
            var user = context.FindWorkflowInput<WorkflowUserContext>(WorkflowsConstants.InputKeys.Actor);
            var authorizer = context.GetRequiredService<IWorkflowAuthorizer>();
            if (!await authorizer.AuthorizeAsync(user, requiredPermission, context.CancellationToken))
            {
                await context.CompleteActivityWithOutcomesAsync("Denied");
                return;
            }
        }

        var payload = context.FindWorkflowInput<IDictionary<string, object>>(WorkflowsConstants.InputKeys.Payload) ?? new Dictionary<string, object>();
        if (!PayloadFilterMatches(PayloadFilter.GetOrDefault(context), payload))
        {
            await context.CompleteActivityWithOutcomesAsync("Skipped");
            return;
        }

        context.SetResult(payload);
        if (context.FindWorkflowInput<string>(WorkflowsConstants.InputKeys.StimulusId) is { Length: > 0 } stimulusId)
        {
            context.JournalData[WorkflowsConstants.InputKeys.StimulusId] = stimulusId;
        }

        await context.CompleteActivityWithOutcomesAsync("Done");
    }

    /// <summary>Every non-empty 'Key = value' line must equal the payload's value for Key, compared as trimmed case-insensitive text.</summary>
    public static bool PayloadFilterMatches(string? filter, IDictionary<string, object> payload)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        foreach (var line in filter.Split(['\n', '\r', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var expected = line[(separator + 1)..].Trim();
            var actual = payload.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase)).Value;
            var actualText = actual switch
            {
                null => string.Empty,
                System.Text.Json.JsonElement element => element.ValueKind == System.Text.Json.JsonValueKind.String ? element.GetString() ?? string.Empty : element.GetRawText(),
                IFormattable formattable => formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
                _ => actual.ToString() ?? string.Empty,
            };
            if (!string.Equals(actualText.Trim(), expected, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
