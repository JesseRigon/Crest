using System.Linq.Expressions;
using System.Security.Claims;
using Crest.Access;
using Crest.Data.Scoping;
using Crest.Contents.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Crest.ContentManagement;
using Crest.ContentManagement.Metadata;
using Crest.ContentManagement.Metadata.Models;
using Crest.ContentManagement.Records;
using Crest.Contents.ViewModels;
using YesSql;
using YesSql.Filters.Query;
using YesSql.Services;

namespace Crest.Contents.Services;

public sealed class DefaultContentsAdminListFilterProvider : IContentsAdminListFilterProvider
{
    /// <summary>The caller's content scope, from the one scope set; the lists never re-derive it.</summary>
    private static async Task<Expression<Func<ContentItemIndex, bool>>> ScopeOfAsync(IServiceProvider services)
    {
        var caller = services.GetRequiredService<ICallerContextAccessor>().Current
            ?? throw new InvalidOperationException("No caller is set for this request.");
        var scopes = await services.GetRequiredService<IScopeSetProvider>().GetAsync(caller);
        return ScopeExpressions.ToPredicate<ContentItemIndex>(scopes.Require(ContentItemScopeProvider.TargetName));
    }

    public void Build(QueryEngineBuilder<ContentItem> builder)
    {
        builder
            .WithNamedTerm("status", builder => builder
                .OneCondition((val, query, ctx) =>
                {
                    var context = (ContentQueryContext)ctx;
                    if (Enum.TryParse<ContentsStatus>(val, true, out var contentsStatus))
                    {
                        switch (contentsStatus)
                        {
                            case ContentsStatus.Draft:
                                query.With<ContentItemIndex>(x => x.Latest && !x.Published);
                                break;
                            case ContentsStatus.Published:
                                query.With<ContentItemIndex>(x => x.Published);
                                break;
                            case ContentsStatus.Owner:
                                var httpContextAccessor = context.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
                                var userNameIdentifier = httpContextAccessor.HttpContext.User?.FindFirstValue(ClaimTypes.NameIdentifier);
                                query.With<ContentItemIndex>(x => x.Owner == userNameIdentifier && x.Latest);
                                break;
                            case ContentsStatus.AllVersions:
                                query.With<ContentItemIndex>(x => x.Latest);
                                break;
                            default:
                                query.With<ContentItemIndex>(x => x.Latest);
                                break;
                        }
                    }
                    else
                    {
                        // Draft is the default value.
                        query.With<ContentItemIndex>(x => x.Latest);
                    }

                    return ValueTask.FromResult<IQuery<ContentItem>>(query);
                })
                .MapTo<ContentOptionsViewModel>((val, model) =>
                {
                    if (Enum.TryParse<ContentsStatus>(val, true, out var contentsStatus))
                    {
                        model.ContentsStatus = contentsStatus;
                    }
                })
                .MapFrom<ContentOptionsViewModel>((model) =>
                {
                    if (model.ContentsStatus != ContentsStatus.Latest)
                    {
                        return (true, model.ContentsStatus.ToString());
                    }

                    return (false, string.Empty);
                })
                .AlwaysRun()
            )
            .WithNamedTerm("sort", builder => builder
                .OneCondition((val, query) =>
                {
                    if (Enum.TryParse<ContentsOrder>(val, true, out var contentsOrder))
                    {
                        switch (contentsOrder)
                        {
                            case ContentsOrder.Modified:
                                query.With<ContentItemIndex>().OrderByDescending(cr => cr.ModifiedUtc).ThenBy(cr => cr.Id);
                                break;
                            case ContentsOrder.Published:
                                query.With<ContentItemIndex>().OrderByDescending(cr => cr.PublishedUtc).ThenBy(cr => cr.Id);
                                break;
                            case ContentsOrder.Created:
                                query.With<ContentItemIndex>().OrderByDescending(cr => cr.CreatedUtc).ThenBy(cr => cr.Id);
                                break;
                            case ContentsOrder.Title:
                                query.With<ContentItemIndex>().OrderBy(cr => cr.DisplayText).ThenBy(cr => cr.Id);
                                break;
                        }
                    }
                    else
                    {
                        // Modified is a default value and applied when there is no filter.
                        query.With<ContentItemIndex>().OrderByDescending(cr => cr.ModifiedUtc).ThenBy(cr => cr.Id);
                    }

                    return query;
                })
                .MapTo<ContentOptionsViewModel>((val, model) =>
                {
                    if (Enum.TryParse<ContentsOrder>(val, true, out var contentsOrder))
                    {
                        model.OrderBy = contentsOrder;
                    }
                })
                .MapFrom<ContentOptionsViewModel>((model) =>
                {
                    if (model.OrderBy != ContentsOrder.Modified)
                    {
                        return (true, model.OrderBy.ToString());
                    }

                    return (false, string.Empty);
                })
                .AlwaysRun()
            )
            .WithNamedTerm("type", builder => builder
                .OneCondition(async (contentType, query, ctx) =>
                {
                    var context = (ContentQueryContext)ctx;
                    var contentDefinitionManager = context.ServiceProvider.GetRequiredService<IContentDefinitionManager>();
                    var scope = await ScopeOfAsync(context.ServiceProvider);

                    // Filter for one or more specific types. We display a specific type even if
                    // it's not listable so that admin pages can reuse the Content list page for
                    // specific types; what the caller may see of them is the scope's business.
                    if (!string.IsNullOrEmpty(contentType))
                    {
                        var requested = new List<string>();
                        foreach (var contentTypeId in contentType.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        {
                            if (await contentDefinitionManager.GetTypeDefinitionAsync(contentTypeId) is not null)
                            {
                                requested.Add(contentTypeId);
                            }
                        }

                        if (requested.Count > 0)
                        {
                            return query.With<ContentItemIndex>(x => x.ContentType.IsIn(requested)).Where(scope);
                        }

                        // At this point, the given content types are invalid. Ignore them.
                    }

                    var listable = (await contentDefinitionManager.ListTypeDefinitionsAsync())
                        .Where(definition => definition.IsListable())
                        .Select(definition => definition.Name)
                        .ToList();

                    return query.With<ContentItemIndex>(x => x.ContentType.IsIn(listable)).Where(scope);
                })
                .MapTo<ContentOptionsViewModel>((val, model) =>
                {
                    if (!string.IsNullOrEmpty(val) && !val.Contains(','))
                    {
                        model.SelectedContentType = val;
                    }
                })
                .MapFrom<ContentOptionsViewModel>((model) =>
                {
                    if (!string.IsNullOrEmpty(model.SelectedContentType))
                    {
                        return (true, model.SelectedContentType);
                    }

                    return (false, string.Empty);
                })
                .AlwaysRun()
            )
            .WithNamedTerm("stereotype", builder => builder
                .OneCondition(async (stereotype, query, ctx) =>
                {
                    var context = (ContentQueryContext)ctx;
                    var contentDefinitionManager = context.ServiceProvider.GetRequiredService<IContentDefinitionManager>();

                    // Filter for one or more stereotypes.
                    if (!string.IsNullOrEmpty(stereotype))
                    {
                        var stereotypes = stereotype.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        var contentTypeDefinitionNames = (await contentDefinitionManager.ListTypeDefinitionsAsync())
                            .Where(definition => stereotypes.Any(value => definition.StereotypeEquals(value, StringComparison.OrdinalIgnoreCase)))
                            .Select(definition => definition.Name)
                            .ToList();

                        // We display a specific type even if it's not listable so that admin pages
                        // can reuse the content list page for specific types.

                        if (contentTypeDefinitionNames.Count > 0)
                        {
                            var scope = await ScopeOfAsync(context.ServiceProvider);
                            return query.With<ContentItemIndex>(x => x.ContentType.IsIn(contentTypeDefinitionNames)).Where(scope);
                        }

                        // At this point, the given stereotypes are invalid. Ignore them.
                    }

                    return query;
                })
            )
            .WithDefaultTerm(ContentsAdminListFilterOptions.DefaultTermName, builder => builder
                .ManyCondition(
                    (val, query) => query.With<ContentItemIndex>(x => x.DisplayText.Contains(val)),
                    (val, query) => query.With<ContentItemIndex>(x => x.DisplayText.NotContains(val))
                )
            );
    }
}
