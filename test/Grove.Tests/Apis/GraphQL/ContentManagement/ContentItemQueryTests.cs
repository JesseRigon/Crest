using Microsoft.Extensions.DependencyInjection;
using Grove.Apis.GraphQL;
using Grove.ContentManagement;
using Grove.ContentManagement.Records;
using Grove.Tests.Apis.Context;
using YesSql;

namespace Grove.Tests.Apis.GraphQL;

public class ContentItemQueryTests
{
    [Fact]
    public async Task Return_ViewContentPermissionIsGranted_Succeeds()
    {
        using var context = new SiteContext()
            .WithRecipe(TestRecipes.Blog)
            .WithPermissionsContext(new PermissionsContext
            {
                UsePermissionsContext = true,
                AuthorizedPermissions =
                [
                    GraphQLPermissions.ExecuteGraphQL,
                    global::Grove.Contents.CommonPermissions.ViewContent,
                ],
            });

        await context.InitializeAsync();

        var contentItemId = await GetPublishedContentItemIdAsync(context, "Blog");

        var result = await context.GraphQLClient.Content
            .Query($@"item: contentItem(contentItemId: ""{contentItemId}"") {{
                contentItemId
            }}");

        Assert.Equal(contentItemId, result["data"]["item"]["contentItemId"].ToString());
    }

    [Fact]
    public async Task Not_ReturnContentItemWithoutViewContentPermission_Succeeds()
    {
        using var context = new SiteContext()
            .WithRecipe(TestRecipes.Blog)
            .WithPermissionsContext(new PermissionsContext
            {
                UsePermissionsContext = true,
                AuthorizedPermissions =
                [
                    GraphQLPermissions.ExecuteGraphQL,
                ],
            });

        await context.InitializeAsync();

        var contentItemId = await GetPublishedContentItemIdAsync(context, "Blog");

        var result = await context.GraphQLClient.Content
            .Query($@"item: contentItem(contentItemId: ""{contentItemId}"") {{
                contentItemId
            }}");

        Assert.Null(result["data"]["item"]);
    }

    private static async Task<string> GetPublishedContentItemIdAsync(SiteContext context, string contentType)
    {
        string contentItemId = null;

        await context.UsingTenantScopeAsync(async scope =>
        {
            var session = scope.ServiceProvider.GetRequiredService<global::YesSql.ISession>();
            var contentItem = await session.Query<ContentItem, ContentItemIndex>(x => x.ContentType == contentType && x.Published).FirstOrDefaultAsync();

            contentItemId = contentItem.ContentItemId;
        });

        return contentItemId;
    }
}
