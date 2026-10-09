using Crest.Workflows.Common.Entities;
using Crest.Workflows.Management.Entities;
using Crest.Workflows.Management.Filters;
using Crest.ContentManagement;
using Crest.Workflows.Indexes;
using YesSql;
using VersionOptions = Crest.Workflows.Common.Models.VersionOptions;

namespace Crest.Workflows.Extensions;

public static class WorkflowDefinitionQueryExtensions
{
    public static IQuery<ContentItem, WorkflowDefinitionIndex> WithVersion(this IQuery<ContentItem, WorkflowDefinitionIndex> query, VersionOptions versionOptions)
    {
        if (versionOptions.IsDraft)
            return query.Where(x => !x.IsPublished);
        if (versionOptions.IsLatest)
            return query.Where(x => x.IsLatest);
        if (versionOptions.IsPublished)
            return query.Where(x => x.IsPublished);
        if (versionOptions.IsLatestOrPublished)
            return query.Where(x => x.IsPublished || x.IsLatest);
        if (versionOptions.IsLatestAndPublished)
            return query.Where(x => x.IsPublished && x.IsLatest);
        if (versionOptions.Version > 0)
            return query.Where(x => x.Version == versionOptions.Version);

        return query;
    }
    
    public static IQuery<ContentItem, WorkflowDefinitionIndex> Apply(this IQuery<ContentItem, WorkflowDefinitionIndex> query, WorkflowDefinitionFilter filter)
    {
        return filter.Apply(query);
    }
    
    public static IQuery<ContentItem, WorkflowDefinitionIndex> Apply<TOrderBy>(this IQuery<ContentItem, WorkflowDefinitionIndex> query, WorkflowDefinitionOrder<TOrderBy> order)
    {
        var keySelector = ExpressionConverter.Convert<WorkflowDefinition, WorkflowDefinitionIndex, TOrderBy>(order.KeySelector);
        return order.Direction == OrderDirection.Ascending 
            ? query.OrderBy(keySelector) 
            : query.OrderByDescending(keySelector);
    }
}