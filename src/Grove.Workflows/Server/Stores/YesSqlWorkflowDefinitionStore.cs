using Grove.Workflows.Common.Models;
using Grove.Workflows.Management;
using Grove.Workflows.Management.Entities;
using Grove.Workflows.Management.Filters;
using Grove.Workflows.Management.Mappers;
using Grove.Workflows.Management.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Records;
using Grove.Workflows.Indexes;
using Grove.Workflows.FieldSets;
using Grove.Workflows.Registry;
using Grove.Workflows.Services;
using Grove.Workflows.Extensions;
using Grove.Modules;
using YesSql;
using VersionOptions = Grove.Contents.Management.VersionOptions;

namespace Grove.Workflows.Stores;

public class YesSqlWorkflowDefinitionStore(
    ISession session,
    IServiceProvider serviceProvider,
    ILogger<YesSqlWorkflowDefinitionStore> logger) : IWorkflowDefinitionStore
{
    private const string Collection = WorkflowCollections.WorkflowDefinitions;

    private readonly Lazy<IContentManager> _contentManager = new(serviceProvider.GetRequiredService<IContentManager>);
    private readonly Lazy<WorkflowDefinitionMapper> _workflowDefinitionMapper = new(serviceProvider.GetRequiredService<WorkflowDefinitionMapper>);
    private readonly Lazy<WorkflowDefinitionFieldSetMapper> _workflowDefinitionFieldSetMapper = new(serviceProvider.GetRequiredService<WorkflowDefinitionFieldSetMapper>);
    private readonly Lazy<WorkflowDefinitionFieldSetSerializer> _workflowDefinitionFieldSetSerializer = new(serviceProvider.GetRequiredService<WorkflowDefinitionFieldSetSerializer>);
    private IEnumerable<IContentHandler>? _contentHandlers;
    private IEnumerable<IContentHandler>? _reversedHandlers;
    private IContentManager ContentManager => _contentManager.Value;
    private WorkflowDefinitionMapper WorkflowDefinitionMapper => _workflowDefinitionMapper.Value;
    private WorkflowDefinitionFieldSetMapper WorkflowDefinitionFieldSetMapper => _workflowDefinitionFieldSetMapper.Value;
    private WorkflowDefinitionFieldSetSerializer WorkflowDefinitionFieldSetSerializer => _workflowDefinitionFieldSetSerializer.Value;
    private IEnumerable<IContentHandler> ContentHandlers => _contentHandlers ??= serviceProvider.GetServices<IContentHandler>().ToArray();
    private IEnumerable<IContentHandler> ReversedHandlers => _reversedHandlers ??= ContentHandlers.Reverse().ToArray();

    public async Task<WorkflowDefinition?> FindAsync(WorkflowDefinitionFilter filter, CancellationToken cancellationToken = default)
    {
        var part = await Query(filter).FirstOrDefaultAsync(cancellationToken);
        return Map(part);
    }

    public async Task<WorkflowDefinition?> FindAsync<TOrderBy>(WorkflowDefinitionFilter filter, WorkflowDefinitionOrder<TOrderBy> order, CancellationToken cancellationToken = default)
    {
        var part = await Query(filter, order).FirstOrDefaultAsync(cancellationToken);
        return Map(part);
    }

    public async Task<Page<WorkflowDefinition>> FindManyAsync(WorkflowDefinitionFilter filter, PageArgs pageArgs, CancellationToken cancellationToken = default)
    {
        var query = Query(filter, pageArgs);
        var count = await query.CountAsync(cancellationToken);
        var parts = await query.ListAsync(cancellationToken);
        var definitions = Map(parts).ToList();

        return Page.Of(definitions, count);
    }

    public async Task<Page<WorkflowDefinition>> FindManyAsync<TOrderBy>(WorkflowDefinitionFilter filter, WorkflowDefinitionOrder<TOrderBy> order, PageArgs pageArgs, CancellationToken cancellationToken = default)
    {
        var query = Query(filter, order, pageArgs);
        var count = await query.CountAsync(cancellationToken);
        var parts = await query.ListAsync(cancellationToken);
        var definitions = Map(parts).ToList();

        return Page.Of(definitions, count);
    }

    public async Task<IEnumerable<WorkflowDefinition>> FindManyAsync(WorkflowDefinitionFilter filter, CancellationToken cancellationToken = default)
    {
        var query = Query(filter);
        var parts = await query.ListAsync(cancellationToken);
        return Map(parts);
    }

    public async Task<IEnumerable<WorkflowDefinition>> FindManyAsync<TOrderBy>(WorkflowDefinitionFilter filter, WorkflowDefinitionOrder<TOrderBy> order, CancellationToken cancellationToken = default)
    {
        var query = Query(filter, order);
        var parts = await query.ListAsync(cancellationToken);
        return Map(parts);
    }

    public async Task<Page<WorkflowDefinitionSummary>> FindSummariesAsync(WorkflowDefinitionFilter filter, PageArgs pageArgs, CancellationToken cancellationToken = default)
    {
        var query = Query(filter, pageArgs);
        var count = await query.CountAsync(cancellationToken);
        var parts = await query.ListAsync(cancellationToken);
        var definitions = MapSummaries(parts).ToList();

        return Page.Of(definitions, count);
    }

    public async Task<Page<WorkflowDefinitionSummary>> FindSummariesAsync<TOrderBy>(WorkflowDefinitionFilter filter, WorkflowDefinitionOrder<TOrderBy> order, PageArgs pageArgs, CancellationToken cancellationToken = default)
    {
        var query = Query(filter, order, pageArgs);
        var count = await query.CountAsync(cancellationToken);
        var parts = await query.ListAsync(cancellationToken);
        var definitions = MapSummaries(parts).ToList();

        return Page.Of(definitions, count);
    }

    public async Task<IEnumerable<WorkflowDefinitionSummary>> FindSummariesAsync(WorkflowDefinitionFilter filter, CancellationToken cancellationToken = default)
    {
        var query = Query(filter);
        var parts = await query.ListAsync(cancellationToken);
        return MapSummaries(parts);
    }

    public async Task<IEnumerable<WorkflowDefinitionSummary>> FindSummariesAsync<TOrderBy>(WorkflowDefinitionFilter filter, WorkflowDefinitionOrder<TOrderBy> order, CancellationToken cancellationToken = default)
    {
        var query = Query(filter, order);
        var parts = await query.ListAsync(cancellationToken);
        return MapSummaries(parts);
    }

    public async Task<WorkflowDefinition?> FindLastVersionAsync(WorkflowDefinitionFilter filter, CancellationToken cancellationToken)
    {
        var part = await Query(filter).OrderByDescending(x => x.Version).FirstOrDefaultAsync(cancellationToken);
        return Map(part);
    }

    public async Task SaveAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default)
    {
        var contentItem = string.IsNullOrEmpty(definition.Id) ? null : await ContentManager.GetVersionAsync(definition.Id);

        if (contentItem == null)
            contentItem = await ContentManager.NewAsync("WorkflowDefinition");

        contentItem.DisplayText = definition.Name;
        await contentItem.AlterAsync<WorkflowDefinitionFieldSet>(async part =>
        {
            var model = await WorkflowDefinitionMapper.MapAsync(definition, cancellationToken);
            WorkflowDefinitionFieldSetMapper.Map(model, part);
            part.DefinitionVersionId = contentItem.ContentItemVersionId;
            part.DefinitionId = contentItem.ContentItemId;
        });

        if (contentItem.ContentItemVersionId == null!)
        {
            await ContentManager.CreateAsync(contentItem, VersionOptions.Draft);
            definition.Id = contentItem.ContentItemVersionId!;

            contentItem.Alter<WorkflowDefinitionFieldSet>(part =>
            {
                part.DefinitionVersionId = definition.Id;
                WorkflowDefinitionFieldSetSerializer.UpdateSerializedData(part);
            });
        }

        await ContentManager.SaveDraftAsync(contentItem);

        definition.Id = contentItem.ContentItemVersionId!;
        definition.DefinitionId = contentItem.ContentItemId!;
    }

    public async Task SaveManyAsync(IEnumerable<WorkflowDefinition> definitions, CancellationToken cancellationToken = default)
    {
        foreach (var definition in definitions)
            await SaveAsync(definition, cancellationToken);
    }

    public async Task<long> DeleteAsync(WorkflowDefinitionFilter filter, CancellationToken cancellationToken = default)
    {
        var query = (await Query(filter).ListAsync(cancellationToken)).ToList();
        var count = 0;

        // System and shipped flows are never deleted by a user (docs/workflows.md, phase 5).
        var guard = serviceProvider.GetRequiredService<WorkflowOwnershipGuard>();
        foreach (var definitionId in query.Select(c => c.ContentItemId).Distinct())
        {
            await guard.AuthorizeChangeAsync(definitionId, WorkflowChange.Delete);
        }

        foreach (var contentItem in query)
        {
            await RemoveContentItemVersionAsync(contentItem, cancellationToken);
            count++;
        }

        await session.FlushAsync(cancellationToken);
        return count;
    }

    public async Task<bool> AnyAsync(WorkflowDefinitionFilter filter, CancellationToken cancellationToken = default)
    {
        return await Query(filter).CountAsync(cancellationToken) > 0;
    }

    public async Task<long> CountDistinctAsync(CancellationToken cancellationToken = default)
    {
        return await session.Query<WorkflowDefinitionFieldSet, WorkflowDefinitionIndex>(collection: Collection).CountAsync(cancellationToken);
    }

    public async Task<bool> GetIsNameUnique(string name, string? definitionId = null, CancellationToken cancellationToken = default)
    {
        var query = session.Query<WorkflowDefinitionFieldSet, WorkflowDefinitionIndex>(collection: Collection).Where(x => x.Name == name);

        if (definitionId != null)
            query = query.Where(x => x.DefinitionId != definitionId);

        return await query.CountAsync(cancellationToken) == 0;
    }

    private IQuery<ContentItem, WorkflowDefinitionIndex> Query(WorkflowDefinitionFilter filter, PageArgs? pageArgs = null)
    {
        return Query<string>(filter, null, pageArgs);
    }

    private IQuery<ContentItem, WorkflowDefinitionIndex> Query<TOrderBy>(WorkflowDefinitionFilter filter, WorkflowDefinitionOrder<TOrderBy>? order = null, PageArgs? pageArgs = null)
    {
        var query = session.Query<ContentItem, WorkflowDefinitionIndex>(collection: Collection).Apply(filter);
        if (order != null) query = query.Apply(order);

        if (pageArgs != null)
        {
            if (pageArgs.Offset != null) query.Skip(pageArgs.Offset.Value);
            if (pageArgs.Limit != null) query.Take(pageArgs.Limit.Value);
        }

        return query;
    }

    private WorkflowDefinition? Map(ContentItem? contentItem)
    {
        return Map(contentItem?.Get<WorkflowDefinitionFieldSet>(ContentFieldSetName.Of<WorkflowDefinitionFieldSet>()));
    }

    private WorkflowDefinition? Map(WorkflowDefinitionFieldSet? part)
    {
        return part == null ? null : WorkflowDefinitionFieldSetMapper.Map(part);
    }

    private IEnumerable<WorkflowDefinition> Map(IEnumerable<ContentItem> contentItems)
    {
        return contentItems.Select(x => WorkflowDefinitionFieldSetMapper.Map(x.Get<WorkflowDefinitionFieldSet>(ContentFieldSetName.Of<WorkflowDefinitionFieldSet>())));
    }

    private IEnumerable<WorkflowDefinitionSummary> MapSummaries(IEnumerable<ContentItem> contentItems)
    {
        return contentItems.Select(x => WorkflowDefinitionFieldSetMapper.MapSummary(x.Get<WorkflowDefinitionFieldSet>(ContentFieldSetName.Of<WorkflowDefinitionFieldSet>())));
    }

    private async Task RemoveContentItemVersionAsync(ContentItem contentItem, CancellationToken cancellationToken = default)
    {
        var removeContext = new RemoveContentContext(contentItem);
        await ContentHandlers.InvokeAsync((handler, context) => handler.RemovingAsync(context), removeContext, logger);
        session.Delete(contentItem, collection: Collection);
        await ReversedHandlers.InvokeAsync((handler, context) => handler.RemovedAsync(context), removeContext, logger);
    }
}