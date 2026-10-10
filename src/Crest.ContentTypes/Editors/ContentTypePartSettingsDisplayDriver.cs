using Crest.ContentManagement.Metadata.Models;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;

namespace Crest.ContentTypes.Editors;

public sealed class ContentTypePartSettingsDisplayDriver : ContentTypePartDefinitionDisplayDriver
{
    public override IDisplayResult Edit(ContentTypePartDefinition model, BuildEditorContext context)
    {
        return Factory("ContentTypePartSettings_Edit", static (ContentTypePartDefinition m) => new ShapeViewModel<ContentTypePartDefinition>(m), model).Location("Content");
    }
}
