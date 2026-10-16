using System.Text.Json;
using System.Text.Json.Nodes;
using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents.Management.Metadata.Settings;
using Grove.Contents.Hierarchy.Services;
using Grove.Contents.Hierarchy.Models;

namespace Grove.Tests.Modules.Grove.Lists.Handlers;

public class HierarchyBehaviourTests
{
    private readonly Mock<IContentDefinitionManager> _contentDefinitionManager;
    private readonly Mock<IServiceProvider> _serviceProvider;
    private readonly HierarchyBehaviour _behaviour;

    public HierarchyBehaviourTests()
    {
        _contentDefinitionManager = new Mock<IContentDefinitionManager>();
        _serviceProvider = new Mock<IServiceProvider>();
        _serviceProvider
            .Setup(sp => sp.GetService(typeof(IContentDefinitionManager)))
            .Returns(_contentDefinitionManager.Object);

        var localizer = new Mock<IStringLocalizer<HierarchyBehaviour>>();
        localizer
            .Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns<string, object[]>((s, args) => new LocalizedString(s, string.Format(s, args)));

        _behaviour = new HierarchyBehaviour(_serviceProvider.Object, null!, localizer.Object);
    }

    [Fact]
    public async Task ListError_ContentTypeIsNotContained_NotFail()
    {
        _contentDefinitionManager
            .Setup(m => m.ListTypeDefinitionsAsync())
            .ReturnsAsync([]);

        var contentItem = new ContentItem { ContentType = "Page" };
        Assert.Null(await _behaviour.ListErrorAsync(contentItem));
    }

    [Fact]
    public async Task ListError_ContainedTypeHasNoParentContentItemFieldSet_NotFail()
    {
        SetupBlogWithBlogPostContainedType();

        var contentItem = new ContentItem { ContentType = "BlogPost" };
        Assert.Null(await _behaviour.ListErrorAsync(contentItem));
    }

    [Fact]
    public async Task ListError_ListContentItemIdIsEmpty_Failss()
    {
        SetupBlogWithBlogPostContainedType();

        var contentItem = new ContentItem { ContentType = "BlogPost" };
        contentItem.Alter<ParentContentItemFieldSet>(p =>
        {
            p.ListContentItemId = string.Empty;
            p.ListContentType = "Blog";
        });

        Assert.NotNull(await _behaviour.ListErrorAsync(contentItem));
    }

    [Fact]
    public async Task ListError_ListContentTypeIsEmpty_NotFail()
    {
        SetupBlogWithBlogPostContainedType();

        var contentItem = new ContentItem { ContentType = "BlogPost" };
        contentItem.Weld<ParentContentItemFieldSet>();
        contentItem.Alter<ParentContentItemFieldSet>(p =>
        {
            p.ListContentItemId = "some-id";
            p.ListContentType = string.Empty;
        });

        Assert.Null(await _behaviour.ListErrorAsync(contentItem));
    }

    [Fact]
    public async Task ListError_ParentContentItemFieldSetIsValid_NotFail()
    {
        SetupBlogWithBlogPostContainedType();

        var contentItem = new ContentItem { ContentType = "BlogPost" };
        contentItem.Weld<ParentContentItemFieldSet>();
        contentItem.Alter<ParentContentItemFieldSet>(p =>
        {
            p.ListContentItemId = "blog-content-item-id";
            p.ListContentType = "Blog";
        });

        Assert.Null(await _behaviour.ListErrorAsync(contentItem));
    }

    [Fact]
    public async Task ListError_ContentTypeIsNotInContainedTypes_NotFail()
    {
        SetupBlogWithBlogPostContainedType();

        var contentItem = new ContentItem { ContentType = "Article" };
        Assert.Null(await _behaviour.ListErrorAsync(contentItem));
    }

    [Fact]
    public async Task ListError_ContentTypeIsCreatableWithoutParentContentItemFieldSet_NotFail()
    {
        SetupBlogWithBlogPostContainedType(creatable: true);

        var contentItem = new ContentItem { ContentType = "BlogPost" };
        Assert.Null(await _behaviour.ListErrorAsync(contentItem));
    }

    [Fact]
    public async Task ListError_CreatableTypeHasEmptyParentContentItemFieldSet_NotFail()
    {
        SetupBlogWithBlogPostContainedType(creatable: true);

        var contentItem = new ContentItem { ContentType = "BlogPost" };
        contentItem.Alter<ParentContentItemFieldSet>(p =>
        {
            p.ListContentItemId = string.Empty;
            p.ListContentType = string.Empty;
        });

        Assert.Null(await _behaviour.ListErrorAsync(contentItem));
    }

    [Fact]
    public async Task ListError_NotCreatableTypeHasPartialParentContentItemFieldSetData_Failss()
    {
        SetupBlogWithBlogPostContainedType(creatable: false);

        var contentItem = new ContentItem { ContentType = "BlogPost" };
        contentItem.Alter<ParentContentItemFieldSet>(p =>
        {
            p.ListContentItemId = string.Empty;
            p.ListContentType = "Blog";
        });

        Assert.NotNull(await _behaviour.ListErrorAsync(contentItem));
    }

    private void SetupBlogWithBlogPostContainedType(bool creatable = false)
    {
        var childContentItemsFieldSetSettings = new JsonObject
        {
            [nameof(ChildContentItemsFieldSetSettings)] = JsonSerializer.SerializeToNode(new ChildContentItemsFieldSetSettings
            {
                ContainedContentTypes = ["BlogPost"],
            }),
        };

        var blogTypeDef = new ContentTypeDefinition(
            "Blog",
            "Blog",
            [
                new ContentTypeFieldSetDefinition(
                    ContentFieldSetName.Of<ChildContentItemsFieldSet>(),
                    new ContentFieldSetDefinition(ContentFieldSetName.Of<ChildContentItemsFieldSet>()),
                    childContentItemsFieldSetSettings),
            ],
            new JsonObject());

        var blogPostTypeSettings = new JsonObject();

        if (creatable)
        {
            blogPostTypeSettings[nameof(ContentTypeSettings)] = JsonSerializer.SerializeToNode(new ContentTypeSettings
            {
                Creatable = true,
            });
        }

        var blogPostTypeDef = new ContentTypeDefinition(
            "BlogPost",
            "Blog Post",
            [],
            blogPostTypeSettings);

        _contentDefinitionManager
            .Setup(m => m.ListTypeDefinitionsAsync())
            .ReturnsAsync([blogTypeDef, blogPostTypeDef]);
    }
}
