using Crest.Workflows.Caching;
using Crest.Workflows.Common.Models;
using Crest.Workflows.Common.Multitenancy;
using Crest.Workflows.Management.Filters;

namespace Crest.Workflows.Management.Services;

/// <inheritdoc />
public class WorkflowDefinitionCacheManager(ICacheManager cache, IHasher hasher, ITenantAccessor tenantAccessor) : IWorkflowDefinitionCacheManager
{
    /// <inheritdoc />
    public ICacheManager Cache => cache;

    /// <inheritdoc />
    public string CreateWorkflowDefinitionVersionCacheKey(string definitionId, VersionOptions versionOptions) => TenantPrefix() + $"WorkflowDefinition:{definitionId}:{versionOptions}";

    /// <inheritdoc />
    public string CreateWorkflowDefinitionFilterCacheKey(WorkflowDefinitionFilter filter)
    {
        var hash = hasher.Hash(filter);
        return TenantPrefix() + $"WorkflowDefinition:{hash}";
    }

    /// <inheritdoc />
    public string CreateWorkflowVersionCacheKey(string definitionId, VersionOptions versionOptions) => TenantPrefix() + $"Workflow:{definitionId}:{versionOptions}";

    /// <inheritdoc />
    public string CreateWorkflowVersionCacheKey(string definitionVersionId) => TenantPrefix() + $"Workflow:{definitionVersionId}";

    /// <inheritdoc />
    public string CreateWorkflowFilterCacheKey(WorkflowDefinitionFilter filter)
    {
        var hash = hasher.Hash(filter);
        return TenantPrefix() + $"Workflow:{hash}";
    }

    /// <inheritdoc />
    public string CreateWorkflowDefinitionVersionCacheKey(string definitionVersionId) => TenantPrefix() + $"WorkflowDefinition:{definitionVersionId}";

    /// <inheritdoc />
    public string CreateWorkflowDefinitionChangeTokenKey(string definitionId) => TenantPrefix() + $"WorkflowChangeToken:{definitionId}";

    /// <inheritdoc />
    public async Task EvictWorkflowDefinitionAsync(string definitionId, CancellationToken cancellationToken = default)
    {
        var changeTokenKey = CreateWorkflowDefinitionChangeTokenKey(definitionId);
        await cache.TriggerTokenAsync(changeTokenKey, cancellationToken);
    }

    private string TenantPrefix()
    {
        var tenantId = tenantAccessor.TenantId;
        return string.IsNullOrEmpty(tenantId) ? string.Empty : $"{tenantId}:";
    }
}