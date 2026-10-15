using Grove.Workflows.State;

namespace Grove.Workflows.Models;

public record InvokeWorkflowResult(WorkflowState WorkflowState, ICollection<Bookmark> Bookmarks);