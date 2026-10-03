using OrchardCore.Data.Migration;
using YesSql.Indexes;
using YesSql.Sql;

namespace Crest.Workflows.Approvals;

public static class ApprovalStatuses
{
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
}

/// <summary>A decision a workflow is waiting for, and who may make it.</summary>
public sealed class ApprovalTask
{
    public long Id { get; set; }
    public string ApprovalId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Orchard role whose members may decide. Either this or <see cref="Permission"/>, or both.</summary>
    public string? Role { get; set; }

    /// <summary>Orchard permission whose holders may decide.</summary>
    public string? Permission { get; set; }

    public string WorkflowInstanceId { get; set; } = string.Empty;
    public string? WorkflowDefinitionId { get; set; }
    public string? CorrelationId { get; set; }
    public string? RequestedBy { get; set; }
    public DateTime CreatedUtc { get; set; }
    public string Status { get; set; } = ApprovalStatuses.Pending;
    public string? DecidedBy { get; set; }
    public string? DecidedByUserId { get; set; }
    public string? Comment { get; set; }
    public DateTime? DecidedUtc { get; set; }
}

public sealed class ApprovalTaskIndex : MapIndex
{
    public string ApprovalId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Role { get; set; }
    public string? Permission { get; set; }
    public string WorkflowInstanceId { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
}

public sealed class ApprovalTaskIndexProvider : IndexProvider<ApprovalTask>
{
    public override void Describe(DescribeContext<ApprovalTask> context) =>
        context.For<ApprovalTaskIndex>().Map(task => new ApprovalTaskIndex
        {
            ApprovalId = task.ApprovalId,
            Status = task.Status,
            Role = task.Role,
            Permission = task.Permission,
            WorkflowInstanceId = task.WorkflowInstanceId,
            CreatedUtc = task.CreatedUtc,
        });
}

public sealed class ApprovalTaskMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ApprovalTaskIndex>(table => table
            .Column<string>(nameof(ApprovalTaskIndex.ApprovalId), c => c.NotNull().WithLength(64))
            .Column<string>(nameof(ApprovalTaskIndex.Status), c => c.NotNull().WithLength(16))
            .Column<string>(nameof(ApprovalTaskIndex.Role), c => c.Nullable().WithLength(255))
            .Column<string>(nameof(ApprovalTaskIndex.Permission), c => c.Nullable().WithLength(255))
            .Column<string>(nameof(ApprovalTaskIndex.WorkflowInstanceId), c => c.NotNull().WithLength(64))
            .Column<DateTime>(nameof(ApprovalTaskIndex.CreatedUtc)));

        await SchemaBuilder.AlterIndexTableAsync<ApprovalTaskIndex>(table =>
        {
            table.CreateIndex("IDX_ApprovalTaskIndex_ApprovalId", nameof(ApprovalTaskIndex.ApprovalId));
            table.CreateIndex("IDX_ApprovalTaskIndex_Status", nameof(ApprovalTaskIndex.Status), nameof(ApprovalTaskIndex.CreatedUtc));
        });

        return 1;
    }
}
