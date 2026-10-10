using Crest.Workflows.Models;

namespace Crest.Workflows.Runtime.Results;

public record WorkflowExecutionResult(
    string WorkflowInstanceId, 
    WorkflowStatus Status, 
    WorkflowSubStatus SubStatus, 
    ICollection<Bookmark> Bookmarks, 
    ICollection<ActivityIncident> Incidents, 
    string? TriggeredActivityId,
    IDictionary<string, object> Output);