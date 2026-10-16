using JetBrains.Annotations;
using Grove.Contents.Management;
using Grove.Workflows.FieldSets;
using YesSql.Indexes;

namespace Grove.Workflows.Indexes;

[UsedImplicitly]
public class WorkflowDefinitionIndexProvider : IndexProvider<ContentItem>
{
    public override void Describe(DescribeContext<ContentItem> context)
    {
        context.For<WorkflowDefinitionIndex>()
            .Map(contentItem =>
            {
                var workflowDefinitionFieldSet = contentItem.Get<WorkflowDefinitionFieldSet>(ContentFieldSetName.Of<WorkflowDefinitionFieldSet>());
                
                if(workflowDefinitionFieldSet == null)
                    return null!;
                
                return new()
                {
                    DefinitionId = contentItem.ContentItemId,
                    DefinitionVersionId = contentItem.ContentItemVersionId,
                    IsLatest = workflowDefinitionFieldSet.ContentItem.Latest,
                    IsPublished = workflowDefinitionFieldSet.ContentItem.Published,
                    IsSystem = workflowDefinitionFieldSet.IsSystem,
                    UsableAsActivity = workflowDefinitionFieldSet.UsableAsActivity,
                    MaterializerName = workflowDefinitionFieldSet.MaterializerName,
                    IsReadonly = workflowDefinitionFieldSet.IsReadonly,
                    Version = workflowDefinitionFieldSet.Version,
                    Name = workflowDefinitionFieldSet.Name,
                    Description = workflowDefinitionFieldSet.Description,
                };
            });
    }
}