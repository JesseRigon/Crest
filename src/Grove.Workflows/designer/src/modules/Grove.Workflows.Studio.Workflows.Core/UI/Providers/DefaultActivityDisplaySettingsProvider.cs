using Grove.Workflows.Studio.Workflows.UI.Contracts;
using Grove.Workflows.Studio.Workflows.UI.Models;
using JetBrains.Annotations;
using MudBlazor;

namespace Grove.Workflows.Studio.Workflows.UI.Providers;

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
        ["Grove.Workflows.NotFoundActivity"] = new(DefaultActivityColors.NotFound, StudioIcons.Heroicons.Exclamation),
        
        // Branching
        ["Grove.Workflows.If"] = new(DefaultActivityColors.Branching, StudioIcons.Heroicons.Question),
        ["Grove.Workflows.FlowDecision"] = new(DefaultActivityColors.Branching, StudioIcons.Heroicons.Question),
        ["Grove.Workflows.Switch"] = new(DefaultActivityColors.Branching, StudioIcons.Tabler.SwitchDiagonal),
        ["Grove.Workflows.FlowSwitch"] = new(DefaultActivityColors.Branching, StudioIcons.Tabler.SwitchDiagonal),
        ["Grove.Workflows.FlowJoin"] = new(DefaultActivityColors.Branching, StudioIcons.Tabler.GitMerge),
        ["Grove.Workflows.FlowFork"] = new(DefaultActivityColors.Branching, StudioIcons.Tabler.GitFork),
        
        // Composition
        ["Grove.Workflows.Complete"] = new(DefaultActivityColors.Composition, StudioIcons.Tabler.CheckCircle),
        ["Grove.Workflows.SetOutput"] = new (DefaultActivityColors.Composition, Icons.Material.Outlined.Output),
        ["Grove.Workflows.DispatchWorkflow"] = new (DefaultActivityColors.Composition, Icons.Material.Outlined.Commit),
        ["Grove.Workflows.BulkDispatchWorkflows"] = new (DefaultActivityColors.Composition, Icons.Material.Outlined.Share),
        ["Grove.Workflows.ExecuteWorkflow"] = new (DefaultActivityColors.Composition, Icons.Material.Outlined.Terminal),
        
        // Console
        ["Grove.Workflows.WriteLine"] = new(DefaultActivityColors.Console, StudioIcons.Tabler.Pencil),
        ["Grove.Workflows.ReadLine"] = new(DefaultActivityColors.Console, StudioIcons.Tabler.Text),
        
        // Email
        ["Grove.Workflows.SendEmail"] = new(DefaultActivityColors.Email, Icons.Material.Outlined.Email),
        
        // Flowchart
        ["Grove.Workflows.Flowchart"] = new(DefaultActivityColors.Flowchart, StudioIcons.Tabler.GitFork),
        ["Grove.Workflows.FlowNode"] = new(DefaultActivityColors.Flowchart, StudioIcons.Tabler.Hexagon),
        ["Grove.Workflows.Start"] = new(DefaultActivityColors.Flowchart, Icons.Material.Outlined.Start),
        ["Grove.Workflows.End"] = new(DefaultActivityColors.Flowchart, Icons.Material.Outlined.OutlinedFlag),
        
        // HTTP
        ["Grove.Workflows.HttpEndpoint"] = new(DefaultActivityColors.Http, StudioIcons.Tabler.Cloud),
        ["Grove.Workflows.WriteHttpResponse"] = new(DefaultActivityColors.Http, StudioIcons.Heroicons.PencilPaper),
        ["Grove.Workflows.WriteFileHttpResponse"] = new(DefaultActivityColors.Http, Icons.Material.Outlined.FileDownload),
        ["Grove.Workflows.SendHttpRequest"] = new(DefaultActivityColors.Http, StudioIcons.Tabler.World),
        ["Grove.Workflows.FlowSendHttpRequest"] = new(DefaultActivityColors.Http, StudioIcons.Tabler.World),
        ["Grove.Workflows.DownloadHttpFile"] = new(DefaultActivityColors.Http, @Icons.Material.Outlined.CloudDownload),
        
        // Looping
        ["Grove.Workflows.While"] = new(DefaultActivityColors.Looping, StudioIcons.Tabler.RepeatOne),
        ["Grove.Workflows.ForEach"] = new(DefaultActivityColors.Looping, StudioIcons.Tabler.RepeatOne),
        ["Grove.Workflows.For"] = new(DefaultActivityColors.Looping, StudioIcons.Tabler.RepeatOne),
        ["Grove.Workflows.ParallelForEach"] = new(DefaultActivityColors.Looping, StudioIcons.Tabler.RepeatOne),
        ["Grove.Workflows.Break"] = new(DefaultActivityColors.Looping, StudioIcons.Tabler.Back1),
        
        // Primitives
        ["Grove.Workflows.SetVariable"] = new(DefaultActivityColors.Primitives, StudioIcons.Tabler.Pencil),
        ["Grove.Workflows.SetName"] = new(DefaultActivityColors.Primitives, StudioIcons.Tabler.Italic),
        ["Grove.Workflows.Finish"] = new(DefaultActivityColors.Primitives, StudioIcons.Tabler.CheckShield),
        ["Grove.Workflows.Fault"] = new(DefaultActivityColors.Primitives, Icons.Material.Outlined.ErrorOutline),
        ["Grove.Workflows.Correlate"] = new(DefaultActivityColors.Primitives, Icons.Material.Outlined.DatasetLinked),
        ["Grove.Workflows.RunTask"] = new(DefaultActivityColors.Primitives, Icons.Material.Outlined.Settings),
        ["Grove.Workflows.PublishEvent"] = new(DefaultActivityColors.Primitives, Icons.Material.Outlined.FlashOn),
        ["Grove.Workflows.Event"] = new(DefaultActivityColors.Primitives, Icons.Material.Outlined.FlashOn),
        
        // Timers
        ["Grove.Workflows.Timer"] = new(DefaultActivityColors.Timer, Icons.Material.Outlined.Timer),
        ["Grove.Workflows.Cron"] = new(DefaultActivityColors.Timer, Icons.Material.Outlined.Timer),
        ["Grove.Workflows.Delay"] = new(DefaultActivityColors.Timer, Icons.Material.Outlined.RotateLeft),
        ["Grove.Workflows.StartAt"] = new(DefaultActivityColors.Timer, Icons.Material.Outlined.CalendarMonth),
        
        // Scripting
        ["Grove.Workflows.RunJavaScript"] = new(DefaultActivityColors.Scripting, Icons.Material.Outlined.Javascript),
        
        // Diagnostics
        ["Grove.Workflows.Log"] = new(DefaultActivityColors.Diagnostics, StudioIcons.Tabler.Pencil),

        // Azure Service Bus
        ["Grove.Workflows.AzureServiceBus.MessageReceived"] = new("#a21caf", StudioIcons.Heroicons.Incoming),
        ["Grove.Workflows.AzureServiceBus.SendMessage"] = new("#a21caf", StudioIcons.Heroicons.Outgoing),
    };
}