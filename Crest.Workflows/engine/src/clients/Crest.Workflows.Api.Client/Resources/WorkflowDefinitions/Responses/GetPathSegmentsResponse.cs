using Crest.Workflows.Api.Client.Shared.Models;

namespace Crest.Workflows.Api.Client.Resources.WorkflowDefinitions.Responses;

public record GetPathSegmentsResponse(ActivityNode ChildNode, ActivityNode Container, ICollection<ActivityPathSegment> PathSegments);