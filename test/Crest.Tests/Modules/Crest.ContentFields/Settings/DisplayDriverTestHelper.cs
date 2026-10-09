using Crest.ContentManagement.Metadata.Builders;
using Crest.ContentManagement.Metadata.Models;
using Crest.ContentTypes.Editors;
using Crest.DisplayManagement;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.DisplayManagement.Zones;

namespace Crest.Tests.Modules.Crest.ContentFields.Settings;

public class DisplayDriverTestHelper
{
    public static ContentPartDefinition GetContentPartDefinition<TField>(Action<ContentPartFieldDefinitionBuilder> configuration)
    {
        return new ContentPartDefinitionBuilder()
            .Named("SomeContentPart")
            .WithField<TField>("SomeField", configuration)
            .Build();
    }

    public static Task<ShapeResult> GetShapeResultAsync(IContentPartFieldDefinitionDisplayDriver driver, IShapeFactory factory, ContentPartDefinition contentDefinition)
        => GetShapeResultAsync(factory, contentDefinition, driver);

    public static Task<ShapeResult> GetShapeResultAsync<TDriver>(IShapeFactory factory, ContentPartDefinition contentDefinition)
        where TDriver : IContentPartFieldDefinitionDisplayDriver, new()
        => GetShapeResultAsync(factory, contentDefinition, new TDriver());

    public static async Task<ShapeResult> GetShapeResultAsync(IShapeFactory factory, ContentPartDefinition contentDefinition, IContentPartFieldDefinitionDisplayDriver driver)
    {
        var partFieldDefinition = contentDefinition.Fields.First();

        var partFieldDefinitionShape = await factory.CreateAsync("ContentPartFieldDefinition_Edit", () =>
            ValueTask.FromResult<IShape>(new ZoneHolding(() => factory.CreateAsync("ContentZone"))));
        partFieldDefinitionShape.Properties["ContentField"] = partFieldDefinition;

        var editorContext = new BuildEditorContext(partFieldDefinitionShape, "", false, "", factory, null, null);

        var result = await driver.BuildEditorAsync(partFieldDefinition, editorContext);
        await result.ApplyAsync(editorContext);

        return (ShapeResult)result;
    }
}
