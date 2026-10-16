using System.Runtime.CompilerServices;
using Grove.Workflows.Common.Entities;
using Grove.Workflows.Common.Models;
using Grove.Workflows.Management;
using Grove.Workflows.Management.Filters;
using Grove.Workflows.Management.Models;

namespace Grove.Workflows.Runtime;

public static class WorkflowInstanceStoreExtensions
{
    public static async IAsyncEnumerable<WorkflowInstanceSummary> EnumerateSummariesAsync(
        this IWorkflowInstanceStore store, 
        WorkflowInstanceFilter filter, 
        int batchSize = 100, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Offset paging needs a stable order, or pages overlap and skip; a short page is the last one.
        var pageArgs = PageArgs.FromPage(0, batchSize);
        var order = new WorkflowInstanceOrder<string>(x => x.Id, OrderDirection.Ascending);

        while (!cancellationToken.IsCancellationRequested)
        {
            var page = await store.SummarizeManyAsync(filter, pageArgs, order, cancellationToken);
            var workflowInstances = page.Items;

            foreach (var workflowInstance in workflowInstances)
                yield return workflowInstance;

            if (workflowInstances.Count < batchSize)
                yield break;

            pageArgs = pageArgs.Next();
        }
    }
}