using Crest.Contents.Services;

namespace Crest.Tests.Modules.Crest.Contents;

public class ContentFilterNodeTests
{
    [Fact]
    public void ContentTypeFilterNodeShouldUseExistingTermForMultipleValues()
    {
        var node = new ContentTypeFilterNode(["Article", "BlogPost"]);

        Assert.Equal("type", node.TermName);
        Assert.Equal("Article,BlogPost", node.Operation.ToString());
    }

    [Fact]
    public void StereotypeFilterNodeShouldUseExistingTermForMultipleValues()
    {
        var node = new StereotypeFilterNode(["Page", "Widget"]);

        Assert.Equal("stereotype", node.TermName);
        Assert.Equal("Page,Widget", node.Operation.ToString());
    }
}
