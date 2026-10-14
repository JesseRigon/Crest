using System.Text.Json.Serialization;
using Crest.Workflows.Api.Models;
using Crest.Workflows.Common.Entities;
using Crest.Workflows.Common.Models;
using Crest.Workflows.Models;
using Crest.Workflows.Serialization.Converters;
using Crest.Validation;
using Microsoft.AspNetCore.Mvc;

namespace Crest.Workflows.Api.Endpoints.WorkflowDefinitions;

/// <summary>The definition list's query string.</summary>
public class ListWorkflowDefinitionsQuery
{
    [FromQuery] public string? VersionOptions { get; set; }

    [FromQuery] public string[]? DefinitionIds { get; set; }

    [FromQuery] public string[]? Ids { get; set; }

    [FromQuery(Name = "materializer")] public string? MaterializerName { get; set; }

    [FromQuery(Name = "label")] public string[]? Labels { get; set; }

    [FromQuery] public int? Page { get; set; }

    [FromQuery] public int? PageSize { get; set; }

    [FromQuery] public OrderByWorkflowDefinition? OrderBy { get; set; }

    [FromQuery] public OrderDirection? OrderDirection { get; set; }

    [FromQuery] public string? SearchTerm { get; set; }

    [FromQuery] public bool? IsSystem { get; set; }
}

public record IsNameUniqueResponse(bool IsUnique);

public record ConsumersResponse(ICollection<string> ConsumingWorkflowDefinitionIds);

public record SubgraphSegmentsResponse(ActivityNode ChildNode, ActivityNode Container, ICollection<ActivityPathSegment> PathSegments);

/// <summary>The answer of a save and of a publish.</summary>
public record PublishResponse(LinkedWorkflowDefinitionModel WorkflowDefinition, bool AlreadyPublished, int ConsumingWorkflowCount);

public record UpdateReferencesResponse(ICollection<string> AffectedWorkflows);

public class DispatchRequest : Validated<DispatchRequest>
{
    public string? InstanceId { get; set; }

    public string? CorrelationId { get; set; }

    public string? TriggerActivityId { get; set; }

    public VersionOptions? VersionOptions { get; set; }

    [JsonConverter(typeof(ExpandoObjectConverterFactory))]
    public object? Input { get; set; }

    public string? Channel { get; set; }

    protected override void Rules(RuleSet<DispatchRequest> rules) =>
        rules.For(x => x.Input).Must(input => input is null or IDictionary<string, object>, "Input must be a dictionary.");
}

public record DispatchResponse(string WorkflowInstanceId);

public class BulkDispatchRequest
{
    public VersionOptions? VersionOptions { get; set; }

    public string? TriggerActivityId { get; set; }

    [JsonConverter(typeof(ExpandoObjectConverterFactory))]
    public object? Input { get; set; }

    public int Count { get; set; } = 1;
}

public record BulkDispatchResponse(ICollection<string> WorkflowInstanceIds);

public class RefreshRequest
{
    public ICollection<string>? DefinitionIds { get; set; }
}

public class RefreshResponse(ICollection<string> refreshed, ICollection<string> notFound)
{
    [JsonPropertyName("refreshed")] public ICollection<string> Refreshed { get; } = refreshed;

    [JsonPropertyName("notFound")] public ICollection<string> NotFound { get; } = notFound;
}

public class BulkDeleteByDefinitionIdsRequest
{
    public ICollection<string>? DefinitionIds { get; set; }
}

public class BulkDeleteByIdsRequest
{
    public ICollection<string>? Ids { get; set; }
}

public class BulkDeleteResponse(long deletedCount)
{
    [JsonPropertyName("deleted")] public long DeletedCount { get; } = deletedCount;
}

public class BulkPublishResponse(ICollection<string> published, ICollection<string> alreadyPublished, ICollection<string> notFound, ICollection<string> skipped, ICollection<string> updatedConsumers)
{
    public ICollection<string> Published { get; } = published;

    public ICollection<string> AlreadyPublished { get; } = alreadyPublished;

    public ICollection<string> NotFound { get; } = notFound;

    public ICollection<string> Skipped { get; } = skipped;

    public ICollection<string> UpdatedConsumers { get; } = updatedConsumers;
}

public class BulkRetractResponse(ICollection<string> retracted, ICollection<string> alreadyRetracted, ICollection<string> notFound, ICollection<string> skipped)
{
    public ICollection<string> Retracted { get; } = retracted;

    public ICollection<string> AlreadyRetracted { get; } = alreadyRetracted;

    public ICollection<string> Skipped { get; } = skipped;

    [Obsolete("Use AlreadyRetracted instead")]
    public ICollection<string> NotPublished { get; } = alreadyRetracted;

    public ICollection<string> NotFound { get; } = notFound;
}
