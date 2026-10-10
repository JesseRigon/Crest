using JetBrains.Annotations;
using Crest.Workflows.Documents;
using YesSql.Indexes;

namespace Crest.Workflows.Indexes;

[UsedImplicitly]
public class ActivityExecutionRecordIndexProvider : IndexProvider<ActivityExecutionRecordDocument>
{
    public ActivityExecutionRecordIndexProvider()
    {
        CollectionName = CrestWorkflowsCollections.ActivityExecutionRecords;
    }

    public override void Describe(DescribeContext<ActivityExecutionRecordDocument> context)
    {
        context.For<ActivityExecutionRecordIndex>().Map(record => new()
        {
            RecordId = record.RecordId,
            WorkflowInstanceId = record.WorkflowInstanceId,
            ActivityNodeId = record.ActivityNodeId,
            ActivityId = record.ActivityId,
            ActivityType = record.ActivityType,
            ActivityTypeVersion = record.ActivityTypeVersion,
            StartedAt = record.StartedAt,
            HasBookmarks = record.HasBookmarks,
            CompletedAt = record.CompletedAt,
            Completed = record.CompletedAt != null,
            Status = record.Status,
            ActivityName = record.ActivityName
        });
    }
}