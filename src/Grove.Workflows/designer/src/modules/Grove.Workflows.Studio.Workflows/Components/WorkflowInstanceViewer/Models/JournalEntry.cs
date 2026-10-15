using Grove.Workflows.Api.Client.Resources.ActivityDescriptors.Models;
using Grove.Workflows.Api.Client.Resources.WorkflowInstances.Models;
using Grove.Workflows.Studio.Workflows.UI.Models;

namespace Grove.Workflows.Studio.Workflows.Pages.WorkflowInstances.View.Models;

/// <summary>
/// Represents the journal entry record.
/// </summary>
public record JournalEntry(
    WorkflowExecutionLogRecord Record,
    ActivityDescriptor? ActivityDescriptor,
    ActivityDisplaySettings? ActivityDisplaySettings,
    bool IsEven,
    TimeSpan TimeMetric
);