using JetBrains.Annotations;
using Crest.Workflows.Documents;
using YesSql.Indexes;

namespace Crest.Workflows.Indexes;

[UsedImplicitly]
public class StoredTriggerIndexProvider : IndexProvider<StoredTriggerDocument>
{
    public StoredTriggerIndexProvider()
    {
        CollectionName = CrestWorkflowsCollections.StoredTriggers;
    }

    public override void Describe(DescribeContext<StoredTriggerDocument> context)
    {
        context.For<StoredTriggerIndex>().Map(document => new()
        {
            TriggerId = document.TriggerId,
            WorkflowDefinitionId = document.WorkflowDefinitionId,
            WorkflowDefinitionVersionId = document.WorkflowDefinitionVersionId,
            Name = document.Name,
            ActivityId = document.ActivityId,
            Hash = document.Hash,
        });
    }
}