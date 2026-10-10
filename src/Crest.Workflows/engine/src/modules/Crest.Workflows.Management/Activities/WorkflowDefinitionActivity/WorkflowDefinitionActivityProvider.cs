using Crest.Workflows.Common.Models;
using Crest.Workflows.Common.Multitenancy;
using Crest.Workflows.Management.Entities;
using Crest.Workflows.Management.Filters;
using Crest.Workflows.Models;

namespace Crest.Workflows.Management.Activities.WorkflowDefinitionActivity;

/// <summary>
/// Provides activity descriptors based on <see cref="WorkflowDefinition"/>s stored in the database.
/// </summary>
public class WorkflowDefinitionActivityProvider(IWorkflowDefinitionStore store, WorkflowDefinitionActivityDescriptorFactory workflowDefinitionActivityDescriptorFactory, ITenantAccessor tenantAccessor) : IActivityProvider
{
    /// <inheritdoc />
    public async ValueTask<IEnumerable<ActivityDescriptor>> GetDescriptorsAsync(CancellationToken cancellationToken = default)
    {
        var filter = new WorkflowDefinitionFilter
        {
            UsableAsActivity = true,
            VersionOptions = VersionOptions.All
        };

        var definitions = (await store.FindManyAsync(filter, cancellationToken)).ToList();
        return CreateDescriptors(definitions).ToList();
    }

    private IEnumerable<ActivityDescriptor> CreateDescriptors(ICollection<WorkflowDefinition> definitions)
    {
        return definitions.Select(x => CreateDescriptor(x, definitions));
    }

    private ActivityDescriptor CreateDescriptor(WorkflowDefinition definition, ICollection<WorkflowDefinition> allDefinitions)
    {
        var latestPublishedVersion = allDefinitions
            .Where(x => x.DefinitionId == definition.DefinitionId && x.IsPublished)
            .MaxBy(x => x.Version);
        return workflowDefinitionActivityDescriptorFactory.CreateDescriptor(definition, latestPublishedVersion);
    }
}