using Crest.Workflows.Api.Client.Resources.ActivityDescriptors.Models;
using Crest.Workflows.Api.Client.Resources.WorkflowInstances.Models;
using Crest.Workflows.Studio.Workflows.UI.Models;

namespace Crest.Workflows.Studio.Workflows.Pages.WorkflowInstances.View.Models;

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