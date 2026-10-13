using System.Linq.Expressions;
using System.Security.Claims;
using Crest.Access;
using Crest.Contents.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Crest.ContentManagement;
using Crest.ContentManagement.Metadata;
using Crest.ContentManagement.Metadata.Builders;
using Crest.ContentManagement.Metadata.Models;
using Crest.ContentManagement.Metadata.Settings;
using Crest.ContentManagement.Records;
using Crest.Contents.Services;
using YesSql;
using YesSql.Filters.Nodes;
using YesSql.Filters.Query;

namespace Crest.Tests.Modules.Crest.Contents;

public class DefaultContentsAdminListFilterProviderTests
{
    private const string UserId = "user-id";

    [Fact]
    public async Task TypeFilterShouldSupportMultipleValuesAndOwnership()
    {
        var definitions = new[]
        {
            CreateContentType("Article"),
            CreateContentType("BlogPost"),
        };

        var predicates = await ExecuteFilterAsync(
            new ContentTypeFilterNode(["Article", "Missing", "BlogPost"]),
            definitions,
            ArticlesOrOwn());

        var article = new ContentItemIndex { Latest = true, ContentType = "Article", Owner = "other-user" };
        var ownedBlogPost = new ContentItemIndex { Latest = true, ContentType = "BlogPost", Owner = UserId };
        var otherBlogPost = new ContentItemIndex { Latest = true, ContentType = "BlogPost", Owner = "other-user" };
        var page = new ContentItemIndex { Latest = true, ContentType = "Page", Owner = UserId };

        Assert.True(Matches(predicates, article));
        Assert.True(Matches(predicates, ownedBlogPost));
        Assert.False(Matches(predicates, otherBlogPost));
        Assert.False(Matches(predicates, page));
    }

    [Fact]
    public async Task StereotypeFilterShouldMatchAnySpecifiedStereotype()
    {
        var definitions = new[]
        {
            CreateContentType("Article", "Page"),
            CreateContentType("HtmlWidget", "Widget"),
            CreateContentType("Menu", "Menu"),
        };

        var predicates = await ExecuteFilterAsync(
            new StereotypeFilterNode(["Page", "Widget"]),
            definitions,
            ArticlesOrOwn());

        var article = new ContentItemIndex { Latest = true, ContentType = "Article", Owner = "other-user" };
        var ownedHtmlWidget = new ContentItemIndex { Latest = true, ContentType = "HtmlWidget", Owner = UserId };
        var otherHtmlWidget = new ContentItemIndex { Latest = true, ContentType = "HtmlWidget", Owner = "other-user" };
        var menu = new ContentItemIndex { Latest = true, ContentType = "Menu", Owner = UserId };

        Assert.True(Matches(predicates, article));
        Assert.True(Matches(predicates, ownedHtmlWidget));
        Assert.False(Matches(predicates, otherHtmlWidget));
        Assert.False(Matches(predicates, menu));
    }

    [Fact]
    public async Task InvalidTypeFilterShouldFallBackToListableTypes()
    {
        var definitions = new[]
        {
            CreateContentType("Article"),
            CreateContentType("Secret", listable: false),
        };

        var predicates = await ExecuteFilterAsync(
            new ContentTypeFilterNode("Missing"),
            definitions,
            ScopeRule.All);

        var article = new ContentItemIndex { Latest = true, ContentType = "Article" };
        var secret = new ContentItemIndex { Latest = true, ContentType = "Secret" };

        Assert.True(Matches(predicates, article));
        Assert.False(Matches(predicates, secret));
    }

    private static async Task<Func<ContentItemIndex, bool>[]> ExecuteFilterAsync(
        TermNode filter,
        ContentTypeDefinition[] definitions,
        ScopeRule scope)
    {
        var definitionManager = new Mock<IContentDefinitionManager>();
        definitionManager
            .Setup(manager => manager.GetTypeDefinitionAsync(It.IsAny<string>()))
            .ReturnsAsync((string name) => definitions.FirstOrDefault(definition => definition.Name == name));
        definitionManager
            .Setup(manager => manager.ListTypeDefinitionsAsync())
            .ReturnsAsync(definitions);

        // What the caller may see is the scope's business (the one scope set), not the filter's.
        var caller = new CallerContext { Tenant = "Default", Side = CallerSide.Admin, UserId = UserId };
        var scopes = new Mock<IScopeSetProvider>();
        scopes
            .Setup(provider => provider.GetAsync(caller, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ScopeSet("test", new Dictionary<string, ScopeRule> { [ContentItemScopeProvider.TargetName] = scope }));

        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, UserId)],
            "Test"));

        using var services = new ServiceCollection()
            .AddSingleton<IHttpContextAccessor>(new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext { User = user },
            })
            .AddSingleton<ICallerContextAccessor>(new Accessor { Current = caller })
            .AddSingleton(scopes.Object)
            .AddSingleton(definitionManager.Object)
            .BuildServiceProvider();

        var predicates = new List<Expression<Func<ContentItemIndex, bool>>>();
        var query = new Mock<IQuery<ContentItem, ContentItemIndex>>();
        query
            .Setup(value => value.With<ContentItemIndex>(It.IsAny<Expression<Func<ContentItemIndex, bool>>>()))
            .Callback((Expression<Func<ContentItemIndex, bool>> predicate) => predicates.Add(predicate))
            .Returns(query.Object);
        query
            .Setup(value => value.With<ContentItemIndex>())
            .Returns(query.Object);
        query
            .Setup(value => value.Where(It.IsAny<Expression<Func<ContentItemIndex, bool>>>()))
            .Callback((Expression<Func<ContentItemIndex, bool>> predicate) => predicates.Add(predicate))
            .Returns(query.Object);
        query
            .Setup(value => value.OrderByDescending(It.IsAny<Expression<Func<ContentItemIndex, object>>>()))
            .Returns(query.Object);
        query
            .Setup(value => value.ThenBy(It.IsAny<Expression<Func<ContentItemIndex, object>>>()))
            .Returns(query.Object);

        var builder = new QueryEngineBuilder<ContentItem>();
        new DefaultContentsAdminListFilterProvider().Build(builder);

        var result = builder.Build().Parse(string.Empty);
        result.TryAddOrReplace(filter);
        await result.ExecuteAsync(new ContentQueryContext(services, query.Object));

        return [.. predicates.Select(CompilePredicate)];
    }

    /// <summary>The query holds every predicate at once (the status term's latest-version one
    /// too): a row is listed when all admit it.</summary>
    private static bool Matches(Func<ContentItemIndex, bool>[] predicates, ContentItemIndex index) =>
        predicates.Length > 0 && predicates.All(predicate => predicate(index));

    /// <summary>The caller may see articles, and their own items of any type.</summary>
    private static ScopeRule ArticlesOrOwn() => ScopeRule.Where(ScopeFilter.AnyOf(
        ScopeFilter.Of(ScopeCondition.In(nameof(ContentItemIndex.ContentType), ["Article"])),
        ScopeFilter.Of(ScopeCondition.Equal(nameof(ContentItemIndex.Owner), UserId))));

    private sealed class Accessor : ICallerContextAccessor
    {
        public CallerContext Current { get; set; }
    }

    private static Func<ContentItemIndex, bool> CompilePredicate(Expression<Func<ContentItemIndex, bool>> predicate)
    {
        var body = new IsInExpressionVisitor().Visit(predicate.Body);

        return Expression.Lambda<Func<ContentItemIndex, bool>>(body, predicate.Parameters).Compile();
    }

    private static ContentTypeDefinition CreateContentType(
        string name,
        string stereotype = null,
        bool listable = true)
    {
        var builder = new ContentTypeDefinitionBuilder()
            .WithName(name)
            .WithDisplayName(name)
            .Listable(listable);

        if (!string.IsNullOrEmpty(stereotype))
        {
            builder.Stereotype(stereotype);
        }

        return builder.Build();
    }

    private sealed class IsInExpressionVisitor : ExpressionVisitor
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.Name != "IsIn")
            {
                return base.VisitMethodCall(node);
            }

            var value = Visit(node.Arguments[0]);
            if (value is UnaryExpression { NodeType: ExpressionType.Convert } convert)
            {
                value = convert.Operand;
            }

            var values = Visit(node.Arguments[1]);
            var enumerableType = typeof(IEnumerable<>).MakeGenericType(value.Type);

            return Expression.Call(
                typeof(Enumerable),
                nameof(Enumerable.Contains),
                [value.Type],
                Expression.Convert(values, enumerableType),
                value);
        }
    }
}
