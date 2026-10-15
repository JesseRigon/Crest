using Grove.Workflows.Api.Client.Shared.Models;

namespace Grove.Workflows.Api.Client.Resources.WorkflowDefinitions.Responses;

public record GetPathSegmentsResponse(ActivityNode ChildNode, ActivityNode Container, ICollection<ActivityPathSegment> PathSegments);