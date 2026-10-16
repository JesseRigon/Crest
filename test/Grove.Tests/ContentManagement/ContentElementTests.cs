using System.Text.Json;
using Grove.Contents.Management;
using Grove.Contents.Title.Models;

namespace Grove.Tests.ContentManagement;

public class ContentElementTests
{
    [Fact]
    public void Get_CastingBaseTypeThenConcreteTypeReturnNewInstance_Succeeds()
    {
        var contentItem = new ContentItem();
        var titleFieldSet = new TitleFieldSet
        {
            Title = "test",
        };

        contentItem.Weld(titleFieldSet);

        var json = JConvert.SerializeObject(contentItem);

        var contentItem2 = JConvert.DeserializeObject<ContentItem>(json);

        // act
        // The order arrangement of the next two calls are important.
        // First cast to ContentFieldSet.
        var contentFieldSet = contentItem2.Get<ContentFieldSet>(ContentFieldSetName.Of<TitleFieldSet>());

        // Second, cast to TitleFieldSet.
        var actualPart = contentItem2.Get<TitleFieldSet>(ContentFieldSetName.Of<TitleFieldSet>());

        // assert
        Assert.NotNull(contentFieldSet);
        Assert.NotNull(actualPart);

        // actualPart should deserialized again, so it must not be same as contentFieldSet.
        Assert.NotSame(contentFieldSet, actualPart);
    }

    [Fact]
    public void Get_CastingConcreteTypeThenBaseTypeReturnNewInstance_Succeeds()
    {
        var contentItem = new ContentItem();
        var titleFieldSet = new TitleFieldSet
        {
            Title = "test",
        };

        contentItem.Weld(titleFieldSet);

        var json = JConvert.SerializeObject(contentItem);

        var contentItem2 = JConvert.DeserializeObject<ContentItem>(json);

        // act
        // The order arrangement of the next two calls are important.
        // First cast to TitleFieldSet.
        var actualPart = contentItem2.Get<TitleFieldSet>(ContentFieldSetName.Of<TitleFieldSet>());

        // Second, cast to ContentFieldSet.
        var contentFieldSet = contentItem2.Get<ContentFieldSet>(ContentFieldSetName.Of<TitleFieldSet>());

        // assert
        Assert.NotNull(contentFieldSet);
        Assert.NotNull(actualPart);

        // contentFieldSet should be returned from cache, so it must be same as actualPart.
        Assert.Same(contentFieldSet, actualPart);
    }

    [Fact]
    public void Apply_CalledWithNullPropertySetThePropertyToNull_Succeeds()
    {
        var contentItem = new ContentItem();

        contentItem.Apply(new TestContentFieldSet()
        {
            Minutes = -15,
        });

        var instance = contentItem.GetOrCreate<TestContentFieldSet>();

        Assert.NotNull(instance);
        Assert.Equal(-15, instance.Minutes);

        contentItem.Apply(new TestContentFieldSet()
        {
            Minutes = null,
        });

        // Using 'dynamic' to bypass object retrieval from the cache when using '.Get()' or '.As()' extension methods.
        var minutes = (int?)contentItem.Content.TestContent.Minutes;

        Assert.Null(minutes);
    }
}

public class TestContentFieldSet : ContentFieldSet
{
    public int? Minutes { get; set; }
}
