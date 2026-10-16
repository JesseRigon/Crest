using System.Text.Json.Nodes;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents.Services;
using Grove.Data.Localization.Data;

namespace Grove.Data.Localization.Translations.Services.Tests;

public class DataLocalizationProviderTests
{
    [Fact]
    public async Task ContentTypeDataLocalizationProvider_GetLocalizedStrings_Succeeds()
    {
        var contentDefinitionManager = new Mock<IContentDefinitionManager>();
        contentDefinitionManager.Setup(cds => cds.ListTypeDefinitionsAsync())
            .ReturnsAsync(() => new List<ContentTypeDefinition> {
                new("Article", "Article"),
                new("BlogPost", "BlogPost"),
                new("News", "News"),
            });
        var dataLocalizationProvider = new ContentTypeDataLocalizationProvider(contentDefinitionManager.Object);
        var localizedStrings = await dataLocalizationProvider.GetDescriptorsAsync();

        Assert.Equal(3, localizedStrings.Count());
        Assert.True(localizedStrings.All(s => s.Context == "Content Types"));
    }

    [Fact]
    public async Task ContentFieldDataLocalizationProvider_GetLocalizedStrings_Succeeds()
    {
        var contentDefinitionManager = new Mock<IContentDefinitionManager>();
        contentDefinitionManager.Setup(cds => cds.ListTypeDefinitionsAsync())
            .ReturnsAsync(() => new List<ContentTypeDefinition>
            {
                CreateContentTypeDefinition("BlogPost", "Blog Post", ["Title", "Body", "Author"]),
                CreateContentTypeDefinition("Person", "Person",  ["FirstName", "LastName"]),
            });
        var dataLocalizationProvider = new ContentFieldDataLocalizationProvider(contentDefinitionManager.Object);
        var localizedStrings = await dataLocalizationProvider.GetDescriptorsAsync();

        Assert.Equal(5, localizedStrings.Count());

        var localizedStringGroups = localizedStrings
            .GroupBy(s => s.Context.Split(Constants.ContextSeparator).Last())
            .ToList();

        Assert.Equal(2, localizedStringGroups.Count);
        Assert.Equal("BlogPost", localizedStringGroups.ElementAt(0).Key);
        Assert.Equal("Person", localizedStringGroups.ElementAt(1).Key);
        Assert.Equal(3, localizedStringGroups.ElementAt(0).Count());
        Assert.Equal(2, localizedStringGroups.ElementAt(1).Count());
    }

    private static ContentTypeDefinition CreateContentTypeDefinition(string name, string displayName, string[] fields)
    {
        var contentFieldDefinitions = new List<ContentFieldDefinition>();
        var settings = new JsonObject();

        foreach (var field in fields)
        {
            contentFieldDefinitions.Add(new ContentFieldDefinition(new ContentFieldTypeDefinition(field), field, settings));
        }

        return new ContentTypeDefinition(
            name,
            displayName,
            new List<ContentTypeFieldSetDefinition>
            {
                new(name, new ContentFieldSetDefinition(name, contentFieldDefinitions, settings), settings),
            },
            settings);
    }
}
