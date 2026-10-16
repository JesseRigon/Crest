using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Routes.Auto.Models;
using Grove.Routes.Auto.Services;
using Grove.Workflows;
using Grove.Workflows.Activities.Flowchart.Attributes;
using Grove.Workflows.Attributes;
using Grove.Workflows.Extensions;
using Grove.Workflows.Models;

namespace Grove.Routes.Auto.Workflows;

/// <summary>Generates an empty permalink from the attachment's pattern and keeps contained items' paths unique. The item is the one the content hook is about (its <c>ContentItem</c> input).</summary>
[Activity("Grove.Routes.Auto", "Content", "Generates an empty permalink from the attachment's pattern and keeps contained items' paths unique.", DisplayName = "Generate route path")]
[FlowNode("Done", "Refused")]
public class GenerateRoutePath : Activity
{
    [Output(Description = "Why the item was refused.")] public Output<string?> Error { get; set; } = null!;

    [Output(Description = "The field the refusal is about.")] public Output<string?> Field { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        await context.GetRequiredService<AutorouteBehaviour>().GenerateAsync(context.GetWorkflowInput<ContentItem>());
        await context.CompleteActivityWithOutcomesAsync("Done");
    }
}

/// <summary>Refuses a permalink that is invalid or already used, or a home page the user may not set. The item is the one the content hook is about (its <c>ContentItem</c> input).</summary>
[Activity("Grove.Routes.Auto", "Content", "Refuses a permalink that is invalid or already used, or a home page the user may not set.", DisplayName = "Check route path")]
[FlowNode("Done", "Refused")]
public class CheckRoutePath : Activity
{
    [Output(Description = "Why the item was refused.")] public Output<string?> Error { get; set; } = null!;

    [Output(Description = "The field the refusal is about.")] public Output<string?> Field { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var refusal = await context.GetRequiredService<AutorouteBehaviour>().RefusalAsync(context.GetWorkflowInput<ContentItem>());
        if (refusal is not { } refused)
        {
            await context.CompleteActivityWithOutcomesAsync("Done");
            return;
        }

        Error.Set(context, refused.Error);
        Field.Set(context, refused.Field);
        await context.CompleteActivityWithOutcomesAsync("Refused");
    }
}

/// <summary>Gives a clone a permalink of its own and never the home page. The item is the one the content hook is about (its <c>ContentItem</c> input).</summary>
[Activity("Grove.Routes.Auto", "Content", "Gives a clone a permalink of its own and never the home page.", DisplayName = "Make route path unique")]
[FlowNode("Done", "Refused")]
public class MakeRoutePathUnique : Activity
{
    [Output(Description = "Why the item was refused.")] public Output<string?> Error { get; set; } = null!;

    [Output(Description = "The field the refusal is about.")] public Output<string?> Field { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        await context.GetRequiredService<AutorouteBehaviour>().CloneAsync(context.GetWorkflowInput<ContentItem>());
        await context.CompleteActivityWithOutcomesAsync("Done");
    }
}

/// <summary>Keeps the route entries, the home route and the route cache when the item is published, unpublished or removed. The item is the one the content hook is about (its <c>ContentItem</c> input).</summary>
[Activity("Grove.Routes.Auto", "Content", "Keeps the route entries, the home route and the route cache when the item is published, unpublished or removed.", DisplayName = "Register route entries")]
[FlowNode("Done", "Refused")]
public class RegisterRouteEntries : Activity
{
    [Output(Description = "Why the item was refused.")] public Output<string?> Error { get; set; } = null!;

    [Output(Description = "The field the refusal is about.")] public Output<string?> Field { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        await context.GetRequiredService<AutorouteBehaviour>().RegisterEntriesAsync(context.GetWorkflowInput<ContentItem>(), context.FindWorkflowInput<string>("ContentLifecyclePoint"));
        await context.CompleteActivityWithOutcomesAsync("Done");
    }
}

/// <summary>Clears a translation's permalink, to be generated again in its culture. The item is the one the content hook is about (its <c>ContentItem</c> input).</summary>
[Activity("Grove.Routes.Auto", "Content", "Clears a translation's permalink, to be generated again in its culture.", DisplayName = "Clear route path")]
[FlowNode("Done", "Refused")]
public class ClearRoutePath : Activity
{
    [Output(Description = "Why the item was refused.")] public Output<string?> Error { get; set; } = null!;

    [Output(Description = "The field the refusal is about.")] public Output<string?> Field { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        await context.GetRequiredService<AutorouteBehaviour>().ClearForTranslationAsync(context.GetWorkflowInput<ContentItem>());
        await context.CompleteActivityWithOutcomesAsync("Done");
    }
}

/// <summary>
/// The permalink's behaviour as system flows (docs/content-engine.md › Behaviour is workflows), attached to every type carrying the Autoroute field set: the path generated on creating, updating, saving a draft and importing; checked on validating; made unique for a clone; the route entries, home route and route cache kept on publishing, unpublishing and removing; cleared for a translation.
/// </summary>
public sealed class AutorouteWorkflowProvider : IWorkflowActivityProvider, IWorkflowFlowProvider, IWorkflowFieldSetHookAttachmentProvider
{
    public const string GenerateFlowKey = "autoroute.generate";
    public const string CheckFlowKey = "autoroute.check";
    public const string CloneFlowKey = "autoroute.clone";
    public const string EntriesFlowKey = "autoroute.entries";
    public const string TranslateFlowKey = "autoroute.translate";

    private static readonly string FieldSet = ContentFieldSetName.Of<AutorouteFieldSet>();

    public IEnumerable<WorkflowActivityDescriptor> Activities =>
    [
        new("autoroute.generate-path", "Generate route path", "content", "Grove.Routes.Auto.GenerateRoutePath", "Generates an empty permalink from the attachment's pattern and keeps contained items' paths unique.", 10, Category: "Permalink"),
        new("autoroute.check-path", "Check route path", "content", "Grove.Routes.Auto.CheckRoutePath", "Refuses a permalink that is invalid or already used, or a home page the user may not set.", 20, Category: "Permalink"),
        new("autoroute.make-unique", "Make route path unique", "content", "Grove.Routes.Auto.MakeRoutePathUnique", "Gives a clone a permalink of its own and never the home page.", 30, Category: "Permalink"),
        new("autoroute.register-entries", "Register route entries", "content", "Grove.Routes.Auto.RegisterRouteEntries", "Keeps the route entries, the home route and the route cache when the item is published, unpublished or removed.", 40, Category: "Permalink"),
        new("autoroute.clear-path", "Clear route path", "content", "Grove.Routes.Auto.ClearRoutePath", "Clears a translation's permalink, to be generated again in its culture.", 50, Category: "Permalink"),
    ];

    public IEnumerable<WorkflowFlowDescriptor> Flows =>
    [
        new(GenerateFlowKey, "Permalink: generate", "content", ShippedFlows.Read(typeof(AutorouteWorkflowProvider).Assembly, "autoroute-generate.json"), 10, Ownership: WorkflowOwnership.System, Version: 1),
        new(CheckFlowKey, "Permalink: check", "content", ShippedFlows.Read(typeof(AutorouteWorkflowProvider).Assembly, "autoroute-check.json"), 20, Ownership: WorkflowOwnership.System, Version: 1),
        new(CloneFlowKey, "Permalink: clone", "content", ShippedFlows.Read(typeof(AutorouteWorkflowProvider).Assembly, "autoroute-clone.json"), 30, Ownership: WorkflowOwnership.System, Version: 1),
        new(EntriesFlowKey, "Permalink: route entries", "content", ShippedFlows.Read(typeof(AutorouteWorkflowProvider).Assembly, "autoroute-entries.json"), 40, Ownership: WorkflowOwnership.System, Version: 1),
        new(TranslateFlowKey, "Permalink: translate", "content", ShippedFlows.Read(typeof(AutorouteWorkflowProvider).Assembly, "autoroute-translate.json"), 50, Ownership: WorkflowOwnership.System, Version: 1),
    ];

    public IEnumerable<WorkflowFieldSetHookAttachmentDescriptor> FieldSetHookAttachments =>
    [
        new(FieldSet, ContentLifecyclePoints.Creating, GenerateFlowKey, 10),
        new(FieldSet, ContentLifecyclePoints.Updating, GenerateFlowKey, 10),
        new(FieldSet, ContentLifecyclePoints.SavingDraft, GenerateFlowKey, 10),
        new(FieldSet, ContentLifecyclePoints.Importing, GenerateFlowKey, 10),
        new(FieldSet, ContentLifecyclePoints.Validating, CheckFlowKey, 10),
        new(FieldSet, ContentLifecyclePoints.Cloning, CloneFlowKey, 10),
        new(FieldSet, ContentLifecyclePoints.Publishing, EntriesFlowKey, 10),
        new(FieldSet, ContentLifecyclePoints.Unpublishing, EntriesFlowKey, 10),
        new(FieldSet, ContentLifecyclePoints.Removing, EntriesFlowKey, 10),
        new(FieldSet, ContentLifecyclePoints.Localizing, TranslateFlowKey, 10),
    ];
}
