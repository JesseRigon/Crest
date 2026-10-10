using GraphQL;
using GraphQL.Types;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Crest.Access;
using Crest.ContentManagement.Records;
using Crest.Contents;
using Crest.Data.Scoping;
using YesSql;

namespace Crest.ContentManagement.GraphQL.Queries;

/// <summary>
/// Applies the caller's content scope (the one scope set) to every content query, and keeps
/// the per-item check after the query as insurance.
/// </summary>
public sealed class ContentItemFilters : GraphQLFilter<ContentItem>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ICallerContextAccessor _callerAccessor;
    private readonly IScopeSetProvider _scopes;
    private readonly IAuthorizationService _authorizationService;

    public ContentItemFilters(
        IHttpContextAccessor httpContextAccessor,
        ICallerContextAccessor callerAccessor,
        IScopeSetProvider scopes,
        IAuthorizationService authorizationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _callerAccessor = callerAccessor;
        _scopes = scopes;
        _authorizationService = authorizationService;
    }

    public override async Task<IQuery<ContentItem>> PreQueryAsync(IQuery<ContentItem> query, IResolveFieldContext context)
    {
        var contentType = ((ListGraphType)context.FieldDefinition.ResolvedType!).ResolvedType!.Name;
        var caller = _callerAccessor.Current
            ?? throw new InvalidOperationException("No caller is set for this request.");
        var scope = (await _scopes.GetAsync(caller)).Require(nameof(ContentItemIndex));

        return query
            .With<ContentItemIndex>(x => x.ContentType == contentType)
            .Where(ScopeExpressions.ToPredicate<ContentItemIndex>(scope));
    }

    public override async Task<IEnumerable<ContentItem>> PostQueryAsync(IEnumerable<ContentItem> contentItems, IResolveFieldContext context)
    {
        var filtered = new List<ContentItem>();
        var user = _httpContextAccessor.HttpContext?.User;

        // The only way to ensure no improper disclosure with certainty is post-query filtering each result with the
        // authorization service. Ideally, pre-query filters should have already done all the work by this point so this
        // is just fall-back insurance.
        foreach (var item in contentItems)
        {
            if (await _authorizationService.AuthorizeAsync(user, CommonPermissions.ViewContent, item))
            {
                filtered.Add(item);
            }
        }

        return filtered;
    }
}
