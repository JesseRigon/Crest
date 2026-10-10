using Crest.Workflows.Management;
using Crest.Workflows.Management.Entities;

// ReSharper disable once CheckNamespace
namespace Crest.Workflows.Extensions;

/// <summary>
/// Provides a set of extension methods for <see cref="IWorkflowInstanceStore"/>.
/// </summary>
public static class WorkflowInstanceStoreExtensions
{
    /// <summary>
    /// Finds a workflow instance matching the specified ID.
    /// </summary>
    public static async ValueTask<WorkflowInstance?> FindAsync(this IWorkflowInstanceStore store, string id, CancellationToken cancellationToken = default)
    {
        return await store.FindAsync(new() { Id = id }, cancellationToken);
    }
}