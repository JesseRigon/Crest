using System.Linq.Expressions;
using System.Text.Json.Serialization;
using Crest.Workflows.Api.Models;
using Crest.Workflows.Common.Entities;
using Crest.Workflows.Common.Models;
using Crest.Workflows.Management.Models;
using Crest.Workflows.Models;
using Crest.Validation;
using Microsoft.AspNetCore.Mvc;

namespace Crest.Workflows.Api.Endpoints.WorkflowInstances;

/// <summary>The instance list's selection: by query string (GET) or by body (POST), the same fields and rules.</summary>
public class ListWorkflowInstancesRequest : Validated<ListWorkflowInstancesRequest>
{
    private static readonly string[] s_timestampColumns = ["CreatedAt", "UpdatedAt", "FinishedAt"];

    public int? Page { get; set; }

    public int? PageSize { get; set; }

    public string? SearchTerm { get; set; }

    public string? DefinitionId { get; set; }

    public ICollection<string>? DefinitionIds { get; set; }

    public string? CorrelationId { get; set; }

    public string? Name { get; set; }

    public int? Version { get; set; }

    public bool? HasIncidents { get; set; }

    public bool? IsSystem { get; set; }

    public WorkflowStatus? Status { get; set; }

    public string[]? Statuses { get; set; }

    public WorkflowSubStatus? SubStatus { get; set; }

    public string[]? SubStatuses { get; set; }

    public OrderByWorkflowInstance? OrderBy { get; set; }

    public OrderDirection? OrderDirection { get; set; }

    public ICollection<TimestampFilter>? TimestampFilters { get; set; }

    protected override void Rules(RuleSet<ListWorkflowInstancesRequest> rules)
    {
        ListRules(rules, x => x.Page, x => x.PageSize, x => x.Statuses, x => x.SubStatuses);
        rules.ForEach(x => x.TimestampFilters, filter => filter.For(x => x.Column)
            .NotEmpty()
            .Must(column => column is null || s_timestampColumns.Contains(column), "{0} must be one of CreatedAt, UpdatedAt and FinishedAt."));
    }

    /// <summary>The rules the GET and the POST form share: the page and the status names.</summary>
    internal static void ListRules<T>(RuleSet<T> rules, Expression<Func<T, int?>> page, Expression<Func<T, int?>> pageSize, Expression<Func<T, string[]?>> statuses, Expression<Func<T, string[]?>> subStatuses)
    {
        rules.For(page).GreaterThanOrEqual(0);
        rules.For(pageSize).GreaterThanOrEqual(1);
        rules.For(statuses).Must(values => values is null || values.All(value => Enum.TryParse<WorkflowStatus>(value, true, out _)), "{0} must be workflow status names.");
        rules.For(subStatuses).Must(values => values is null || values.All(value => Enum.TryParse<WorkflowSubStatus>(value, true, out _)), "{0} must be workflow sub-status names.");
    }
}

/// <summary>The GET form of the instance list: the simple fields of <see cref="ListWorkflowInstancesRequest"/>, the same rules.</summary>
public class ListWorkflowInstancesQuery : Validated<ListWorkflowInstancesQuery>
{
    protected override void Rules(RuleSet<ListWorkflowInstancesQuery> rules) =>
        ListWorkflowInstancesRequest.ListRules(rules, x => x.Page, x => x.PageSize, x => x.Statuses, x => x.SubStatuses);

    [FromQuery] public int? Page { get; set; }

    [FromQuery] public int? PageSize { get; set; }

    [FromQuery] public string? SearchTerm { get; set; }

    [FromQuery] public string? DefinitionId { get; set; }

    [FromQuery] public string[]? DefinitionIds { get; set; }

    [FromQuery] public string? CorrelationId { get; set; }

    [FromQuery] public string? Name { get; set; }

    [FromQuery] public int? Version { get; set; }

    [FromQuery] public bool? HasIncidents { get; set; }

    [FromQuery] public bool? IsSystem { get; set; }

    [FromQuery] public WorkflowStatus? Status { get; set; }

    [FromQuery] public string[]? Statuses { get; set; }

    [FromQuery] public WorkflowSubStatus? SubStatus { get; set; }

    [FromQuery] public string[]? SubStatuses { get; set; }

    [FromQuery] public OrderByWorkflowInstance? OrderBy { get; set; }

    [FromQuery] public OrderDirection? OrderDirection { get; set; }

    public ListWorkflowInstancesRequest ToRequest() => new()
    {
        Page = Page,
        PageSize = PageSize,
        SearchTerm = SearchTerm,
        DefinitionId = DefinitionId,
        DefinitionIds = DefinitionIds,
        CorrelationId = CorrelationId,
        Name = Name,
        Version = Version,
        HasIncidents = HasIncidents,
        IsSystem = IsSystem,
        Status = Status,
        Statuses = Statuses,
        SubStatus = SubStatus,
        SubStatuses = SubStatuses,
        OrderBy = OrderBy,
        OrderDirection = OrderDirection,
    };
}

public class ListWorkflowInstancesResponse(ICollection<WorkflowInstanceSummary> items, long totalCount)
{
    public ICollection<WorkflowInstanceSummary> Items { get; set; } = items;

    public long TotalCount { get; set; } = totalCount;
}

public record ExecutionStateResponse(WorkflowStatus Status, WorkflowSubStatus SubStatus, DateTimeOffset UpdatedAt);

public class JournalPageQuery
{
    [FromQuery] public int? Page { get; set; }

    [FromQuery] public int? PageSize { get; set; }

    [FromQuery] public int? Skip { get; set; }

    [FromQuery] public int? Take { get; set; }
}

public class FilteredJournalRequest
{
    public JournalFilter? Filter { get; set; }

    public int? Page { get; set; }

    public int? PageSize { get; set; }

    public int? Skip { get; set; }

    public int? Take { get; set; }
}

public class JournalFilter
{
    public ICollection<string>? ActivityIds { get; set; }

    public ICollection<string>? ActivityNodeIds { get; set; }

    public ICollection<string>? ExcludedActivityTypes { get; set; }

    public ICollection<string>? EventNames { get; set; }
}

internal class JournalResponse(ICollection<ExecutionLogRecord> items, long totalCount)
{
    public ICollection<ExecutionLogRecord> Items { get; } = items;

    public long TotalCount { get; } = totalCount;
}

public record ResolvedVariableModel(string Id, string Name, object? Value);

public class SetVariablesRequest
{
    public ICollection<VariableUpdateValue>? Variables { get; set; }
}

/// <summary>Cancels by instance ids, by a definition version, or by a definition at a version.</summary>
public class BulkCancelRequest : Validated<BulkCancelRequest>
{
    public ICollection<string>? Ids { get; set; }

    public string? DefinitionVersionId { get; set; }

    /// <summary>Goes with <see cref="VersionOptions"/>.</summary>
    public string? DefinitionId { get; set; }

    /// <summary>Goes with <see cref="DefinitionId"/>.</summary>
    public VersionOptions? VersionOptions { get; set; }

    protected override void Rules(RuleSet<BulkCancelRequest> rules) =>
        rules.Must(x => (x.DefinitionId is null) == (x.VersionOptions is null), "VersionOptions and DefinitionId should either both be empty or both be not empty", nameof(VersionOptions));
}

public class BulkCancelResponse(int cancelledCount)
{
    [JsonPropertyName("cancelled")] public int CancelledCount { get; } = cancelledCount;
}

public class BulkDeleteInstancesRequest
{
    public ICollection<string>? Ids { get; set; }

    public string? WorkflowDefinitionId { get; set; }

    public ICollection<string>? WorkflowDefinitionIds { get; set; }
}

public class BulkDeleteInstancesResponse(long deletedCount)
{
    [JsonPropertyName("deleted")] public long DeletedCount { get; } = deletedCount;
}
