using Grove.ContentManagement;
using Grove.ContentManagement.Handlers;
using Grove.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Workflows.Samples;

// Workflow fixtures of Grove's own (docs/architecture.md › Still to build, 6: workflow browser
// checks written against a Grove fixture instead of a host's documents). Off unless a test or
// demo enables the feature. With Grove.Parties.SampleRole they give the checks a party role,
// a content-published trigger, a system flow and a shipped template - all a host's modules
// would otherwise have to supply.
[Feature(WorkflowSamples.FeatureId)]
public sealed class WorkflowSamplesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IWorkflowTriggerProvider, WorkflowSamplesProvider>();
        services.AddScoped<IWorkflowFlowProvider, WorkflowSamplesProvider>();
        services.AddScoped<IContentHandler, SampleContentPublishedHandler>();
    }
}

public static class WorkflowSamples
{
    public const string FeatureId = "Grove.Workflows.Samples";

    /// <summary>Raised for every published content item: payload ContentItemId, ContentType, DisplayText. Correlated to the item.</summary>
    public const string ContentPublishedTrigger = "sample.content-published";

    /// <summary>System flow: read-only for everyone, published, writes one line per published item.</summary>
    public const string SystemFlowKey = "samples.content-published";

    /// <summary>Shipped template: ships as a draft, forks on the first save, resets to the shipped version.</summary>
    public const string ShippedFlowKey = "samples.template";

    public const string Object = WorkflowsConstants.Objects.Content;

    internal static string Definition(string name, string description, string line) => $$"""
        {
          "name": "{{name}}",
          "description": "{{description}}",
          "root": {
            "type": "Grove.Workflows.Flowchart", "id": "flow", "version": 1,
            "activities": [
              { "type": "Grove.Workflows.RegistryTrigger", "id": "published", "version": 1,
                "triggerKey": { "typeName": "String", "expression": { "type": "Literal", "value": "{{ContentPublishedTrigger}}" } },
                "customProperties": { "canStartWorkflow": true } },
              { "type": "Grove.Workflows.WriteLine", "id": "log", "version": 1,
                "text": { "typeName": "String", "expression": { "type": "Literal", "value": "{{line}}" } } }
            ],
            "connections": [ { "source": { "activity": "published", "port": "Done" }, "target": { "activity": "log", "port": "In" } } ]
          }
        }
        """;
}

public sealed class WorkflowSamplesProvider : IWorkflowTriggerProvider, IWorkflowFlowProvider
{
    public IEnumerable<WorkflowTriggerDescriptor> Triggers =>
    [
        new(WorkflowSamples.ContentPublishedTrigger, "Content published (sample)", WorkflowSamples.Object, "A content item was published: payload ContentItemId, ContentType, DisplayText.", 900),
    ];

    public IEnumerable<WorkflowFlowDescriptor> Flows =>
    [
        new(WorkflowSamples.SystemFlowKey, "Content published (sample)", WorkflowSamples.Object,
            WorkflowSamples.Definition("Content published (sample)", "The samples' system flow: one line per published item.", "Content published."),
            900, Ownership: WorkflowOwnership.System, Version: 1),
        new(WorkflowSamples.ShippedFlowKey, "Sample template", WorkflowSamples.Object,
            WorkflowSamples.Definition("Sample template", "The samples' shipped template: a draft to fork.", "Sample template ran."),
            910, Publish: false, Version: 1),
    ];
}

/// <summary>Raises <see cref="WorkflowSamples.ContentPublishedTrigger"/> after the publishing unit commits.</summary>
public sealed class SampleContentPublishedHandler(IWorkflowTriggerPublisher triggers) : ContentHandlerBase
{
    public override Task PublishedAsync(PublishContentContext context) =>
        triggers.PublishAsync(WorkflowSamples.ContentPublishedTrigger, context.ContentItem.ContentItemId, new Dictionary<string, object>
        {
            ["ContentItemId"] = context.ContentItem.ContentItemId,
            ["ContentType"] = context.ContentItem.ContentType,
            ["DisplayText"] = context.ContentItem.DisplayText ?? string.Empty,
        });
}
