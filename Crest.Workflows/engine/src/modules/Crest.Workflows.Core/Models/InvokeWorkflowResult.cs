using Crest.Workflows.State;

namespace Crest.Workflows.Models;

public record InvokeWorkflowResult(WorkflowState WorkflowState, ICollection<Bookmark> Bookmarks);