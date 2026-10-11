using Crest.Workflows.Abstractions;
using Crest.Workflows.Common.Entities;
using Crest.Workflows.Common.Models;
using Crest.Workflows.Models;
using Crest.Workflows.Api.Models;
using Crest.Workflows.Management;
using Crest.Workflows.Management.Filters;
using Crest.Workflows.Management.Models;
using JetBrains.Annotations;

namespace Crest.Workflows.Api.Endpoints.WorkflowDefinitions.List;

[PublicAPI]
internal class List(IWorkflowDefinitionStore store, IWorkflowDefinitionLinker linker) : WorkflowsEndpoint<Request, PagedListResponse<LinkedWorkflowDefinitionSummary>>
{
    public override void Configure()
    {
        Get("/workflow-definitions");
        ConfigurePermissions("read:workflow-definitions");
    }

    public override async Task<PagedListResponse<LinkedWorkflowDefinitionSummary>> ExecuteAsync(Request request, CancellationToken cancellationToken)
    {
        var pageArgs = PageArgs.FromPage(request.Page, request.PageSize);
        var filter = CreateFilter(request);
        var summaries = await FindAsync(request, filter, pageArgs, cancellationToken);
        var pagedList = new PagedListResponse<WorkflowDefinitionSummary>(summaries);
        var response = linker.MapAsync(pagedList);
        return response;
    }

    private WorkflowDefinitionFilter CreateFilter(Request request)
    {
        var versionOptions = string.IsNullOrWhiteSpace(request.VersionOptions) ? default(VersionOptions?) : VersionOptions.FromString(request.VersionOptions);

        return new()
        {
            IsSystem = request.IsSystem,
            VersionOptions = versionOptions,
            SearchTerm = request.SearchTerm?.Trim(),
            MaterializerName = request.MaterializerName,
            DefinitionIds = request.DefinitionIds,
            Ids = request.Ids
        };
    }

    private async Task<Page<WorkflowDefinitionSummary>> FindAsync(Request request, WorkflowDefinitionFilter filter, PageArgs pageArgs, CancellationToken cancellationToken)
    {
        request.OrderBy ??= OrderByWorkflowDefinition.Name;

        var direction = request.OrderBy == OrderByWorkflowDefinition.Name ? request.OrderDirection ?? OrderDirection.Ascending : request.OrderDirection ?? OrderDirection.Descending;

        switch (request.OrderBy)
        {
            default:
                {
                    var order = new WorkflowDefinitionOrder<DateTimeOffset>
                    {
                        KeySelector = p => p.CreatedAt,
                        Direction = direction
                    };

                    return await store.FindSummariesAsync(filter, order, pageArgs, cancellationToken);
                }
            case OrderByWorkflowDefinition.Name:
                {
                    var order = new WorkflowDefinitionOrder<string>
                    {
                        KeySelector = p => p.Name!,
                        Direction = direction
                    };

                    return await store.FindSummariesAsync(filter, order, pageArgs, cancellationToken);
                }
            case OrderByWorkflowDefinition.Version:
                {
                    var order = new WorkflowDefinitionOrder<int>
                    {
                        KeySelector = p => p.Version,
                        Direction = direction
                    };

                    return await store.FindSummariesAsync(filter, order, pageArgs, cancellationToken);
                }
        }
    }
}