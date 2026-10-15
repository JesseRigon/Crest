using Grove.Contents.Management;
using Grove.Contents.Management.Models;
using Grove.Contents.Feeds.Builders;
using Grove.Data.Feeds.Models;
using Grove.Data.Feeds.Rss;

namespace Grove.Tests.Modules.Contents.Feeds;

public class RssFeedTests
{
    [Theory]
    [InlineData("rss")]
    [InlineData("non rss")]
    public async Task AvoidDoubleEncodeCDATA_Default_Succeeds(string format)
    {
        // Arrange
        var contentManagerMock = new Mock<IContentManager>();
        var commonFeedItemBuilder = new CommonFeedItemBuilder(contentManagerMock.Object);
        var feedContext = CreateFeedContext(format);

        contentManagerMock.SetReturnsDefault(Task.FromResult(new ContentItemMetadata
        {
            DisplayRouteValues = [],
        }));

        contentManagerMock.SetReturnsDefault(Task.FromResult(new BodyAspect
        {
            Body = new HtmlString("<p>The news description goes here ...</p>"),
        }));

        feedContext.Builder.AddItem(feedContext, new ContentItem
        {
            DisplayText = "News",
            PublishedUtc = DateTime.UtcNow,
        });

        // Act
        await commonFeedItemBuilder.PopulateAsync(feedContext);

        // Assert
        var description = feedContext.Response.Items[0].Element.Element("description").ToString();

        Assert.NotEqual("<description>&lt;![CDATA[&lt;p&gt;The news description goes here ...&lt;/p&gt;]]&gt;</description>", description);
        Assert.Equal("<description><![CDATA[<p>The news description goes here ...</p>]]></description>", description);
    }

    [Theory]
    [InlineData("rss")]
    [InlineData("non rss")]
    public async Task Only_HtmlEntityEscapeFeedTitle_Succeeds(string format)
    {
        // Arrange
        var contentManagerMock = new Mock<IContentManager>();
        var commonFeedItemBuilder = new CommonFeedItemBuilder(contentManagerMock.Object);
        var feedContext = CreateFeedContext(format);

        contentManagerMock.SetReturnsDefault(Task.FromResult(new ContentItemMetadata
        {
            DisplayRouteValues = [],
        }));

        contentManagerMock.SetReturnsDefault(Task.FromResult(new BodyAspect
        {
            Body = new HtmlString("<p>The news description goes here ...</p>"),
        }));

        feedContext.Builder.AddItem(feedContext, new ContentItem
        {
            DisplayText = "It's a great title & so much > than anybody's!",
            PublishedUtc = DateTime.UtcNow,
        });

        // Act
        await commonFeedItemBuilder.PopulateAsync(feedContext);

        // Assert
        var title = feedContext.Response.Items[0].Element.Element("title").ToString();

        // Test to ensure that double encoding of title does not occur and complies with XML requirements
        Assert.Equal("<title>It's a great title &amp; so much &gt; than anybody's!</title>", title);
    }

    private static FeedContext CreateFeedContext(string format)
    {
        var feedContext = new FeedContext(new Microsoft.AspNetCore.Http.DefaultHttpContext(), format);

        feedContext.Response.Element = new XElement("Feed");
        feedContext.Builder = new RssFeedBuilder();

        return feedContext;
    }
}
