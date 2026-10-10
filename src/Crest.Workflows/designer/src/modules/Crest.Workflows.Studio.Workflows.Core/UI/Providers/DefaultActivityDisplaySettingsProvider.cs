using Crest.Workflows.Studio.Workflows.UI.Contracts;
using Crest.Workflows.Studio.Workflows.UI.Models;
using JetBrains.Annotations;
using MudBlazor;

namespace Crest.Workflows.Studio.Workflows.UI.Providers;

/// Provides default activity display settings.
[UsedImplicitly]
/// <summary>
/// Provides default activity display settings services.
/// </summary>
public class DefaultActivityDisplaySettingsProvider : IActivityDisplaySettingsProvider
{
    /// <inheritdoc />
    public IDictionary<string, ActivityDisplaySettings> GetSettings() => new Dictionary<string, ActivityDisplaySettings>
    {
        // Not Found Activity
        ["Crest.Workflows.NotFoundActivity"] = new(DefaultActivityColors.NotFound, CrestWorkflowsStudioIcons.Heroicons.Exclamation),
        
        // Branching
        ["Crest.Workflows.If"] = new(DefaultActivityColors.Branching, CrestWorkflowsStudioIcons.Heroicons.Question),
        ["Crest.Workflows.FlowDecision"] = new(DefaultActivityColors.Branching, CrestWorkflowsStudioIcons.Heroicons.Question),
        ["Crest.Workflows.Switch"] = new(DefaultActivityColors.Branching, CrestWorkflowsStudioIcons.Tabler.SwitchDiagonal),
        ["Crest.Workflows.FlowSwitch"] = new(DefaultActivityColors.Branching, CrestWorkflowsStudioIcons.Tabler.SwitchDiagonal),
        ["Crest.Workflows.FlowJoin"] = new(DefaultActivityColors.Branching, CrestWorkflowsStudioIcons.Tabler.GitMerge),
        ["Crest.Workflows.FlowFork"] = new(DefaultActivityColors.Branching, CrestWorkflowsStudioIcons.Tabler.GitFork),
        
        // Composition
        ["Crest.Workflows.Complete"] = new(DefaultActivityColors.Composition, CrestWorkflowsStudioIcons.Tabler.CheckCircle),
        ["Crest.Workflows.SetOutput"] = new (DefaultActivityColors.Composition, Icons.Material.Outlined.Output),
        ["Crest.Workflows.DispatchWorkflow"] = new (DefaultActivityColors.Composition, Icons.Material.Outlined.Commit),
        ["Crest.Workflows.BulkDispatchWorkflows"] = new (DefaultActivityColors.Composition, Icons.Material.Outlined.Share),
        ["Crest.Workflows.ExecuteWorkflow"] = new (DefaultActivityColors.Composition, Icons.Material.Outlined.Terminal),
        
        // Console
        ["Crest.Workflows.WriteLine"] = new(DefaultActivityColors.Console, CrestWorkflowsStudioIcons.Tabler.Pencil),
        ["Crest.Workflows.ReadLine"] = new(DefaultActivityColors.Console, CrestWorkflowsStudioIcons.Tabler.Text),
        
        // Email
        ["Crest.Workflows.SendEmail"] = new(DefaultActivityColors.Email, Icons.Material.Outlined.Email),
        
        // Flowchart
        ["Crest.Workflows.Flowchart"] = new(DefaultActivityColors.Flowchart, CrestWorkflowsStudioIcons.Tabler.GitFork),
        ["Crest.Workflows.FlowNode"] = new(DefaultActivityColors.Flowchart, CrestWorkflowsStudioIcons.Tabler.Hexagon),
        ["Crest.Workflows.Start"] = new(DefaultActivityColors.Flowchart, Icons.Material.Outlined.Start),
        ["Crest.Workflows.End"] = new(DefaultActivityColors.Flowchart, Icons.Material.Outlined.OutlinedFlag),
        
        // HTTP
        ["Crest.Workflows.HttpEndpoint"] = new(DefaultActivityColors.Http, CrestWorkflowsStudioIcons.Tabler.Cloud),
        ["Crest.Workflows.WriteHttpResponse"] = new(DefaultActivityColors.Http, CrestWorkflowsStudioIcons.Heroicons.PencilPaper),
        ["Crest.Workflows.WriteFileHttpResponse"] = new(DefaultActivityColors.Http, Icons.Material.Outlined.FileDownload),
        ["Crest.Workflows.SendHttpRequest"] = new(DefaultActivityColors.Http, CrestWorkflowsStudioIcons.Tabler.World),
        ["Crest.Workflows.FlowSendHttpRequest"] = new(DefaultActivityColors.Http, CrestWorkflowsStudioIcons.Tabler.World),
        ["Crest.Workflows.DownloadHttpFile"] = new(DefaultActivityColors.Http, @Icons.Material.Outlined.CloudDownload),
        
        // Looping
        ["Crest.Workflows.While"] = new(DefaultActivityColors.Looping, CrestWorkflowsStudioIcons.Tabler.RepeatOne),
        ["Crest.Workflows.ForEach"] = new(DefaultActivityColors.Looping, CrestWorkflowsStudioIcons.Tabler.RepeatOne),
        ["Crest.Workflows.For"] = new(DefaultActivityColors.Looping, CrestWorkflowsStudioIcons.Tabler.RepeatOne),
        ["Crest.Workflows.ParallelForEach"] = new(DefaultActivityColors.Looping, CrestWorkflowsStudioIcons.Tabler.RepeatOne),
        ["Crest.Workflows.Break"] = new(DefaultActivityColors.Looping, CrestWorkflowsStudioIcons.Tabler.Back1),
        
        // Primitives
        ["Crest.Workflows.SetVariable"] = new(DefaultActivityColors.Primitives, CrestWorkflowsStudioIcons.Tabler.Pencil),
        ["Crest.Workflows.SetName"] = new(DefaultActivityColors.Primitives, CrestWorkflowsStudioIcons.Tabler.Italic),
        ["Crest.Workflows.Finish"] = new(DefaultActivityColors.Primitives, CrestWorkflowsStudioIcons.Tabler.CheckShield),
        ["Crest.Workflows.Fault"] = new(DefaultActivityColors.Primitives, Icons.Material.Outlined.ErrorOutline),
        ["Crest.Workflows.Correlate"] = new(DefaultActivityColors.Primitives, Icons.Material.Outlined.DatasetLinked),
        ["Crest.Workflows.RunTask"] = new(DefaultActivityColors.Primitives, Icons.Material.Outlined.Settings),
        ["Crest.Workflows.PublishEvent"] = new(DefaultActivityColors.Primitives, Icons.Material.Outlined.FlashOn),
        ["Crest.Workflows.Event"] = new(DefaultActivityColors.Primitives, Icons.Material.Outlined.FlashOn),
        
        // Timers
        ["Crest.Workflows.Timer"] = new(DefaultActivityColors.Timer, Icons.Material.Outlined.Timer),
        ["Crest.Workflows.Cron"] = new(DefaultActivityColors.Timer, Icons.Material.Outlined.Timer),
        ["Crest.Workflows.Delay"] = new(DefaultActivityColors.Timer, Icons.Material.Outlined.RotateLeft),
        ["Crest.Workflows.StartAt"] = new(DefaultActivityColors.Timer, Icons.Material.Outlined.CalendarMonth),
        
        // Scripting
        ["Crest.Workflows.RunJavaScript"] = new(DefaultActivityColors.Scripting, Icons.Material.Outlined.Javascript),
        
        // Diagnostics
        ["Crest.Workflows.Log"] = new(DefaultActivityColors.Diagnostics, CrestWorkflowsStudioIcons.Tabler.Pencil),

        // Azure Service Bus
        ["Crest.Workflows.AzureServiceBus.MessageReceived"] = new("#a21caf", CrestWorkflowsStudioIcons.Heroicons.Incoming),
        ["Crest.Workflows.AzureServiceBus.SendMessage"] = new("#a21caf", CrestWorkflowsStudioIcons.Heroicons.Outgoing),
    };
}