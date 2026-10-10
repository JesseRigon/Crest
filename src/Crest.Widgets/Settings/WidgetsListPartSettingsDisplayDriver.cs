using Crest.ContentManagement.Metadata.Models;
using Crest.ContentTypes.Editors;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Widgets.Models;

namespace Crest.Widgets.Settings;

public sealed class WidgetsListPartSettingsDisplayDriver : ContentTypePartDefinitionDisplayDriver<WidgetsListPart>
{
    private static readonly char[] s_separator = [',', ' '];

    public override IDisplayResult Edit(ContentTypePartDefinition contentTypePartDefinition, BuildEditorContext context)
    {
        return Initialize<WidgetsListPartSettingsViewModel>("WidgetsPartSettings_Edit", model =>
        {
            var settings = contentTypePartDefinition.GetSettings<WidgetsListPartSettings>();

            model.Zones = string.Join(", ", settings.Zones);
            model.WidgetsListPartSettings = settings;
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentTypePartDefinition contentTypePartDefinition, UpdateTypePartEditorContext context)
    {
        var model = new WidgetsListPartSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.Zones);

        context.Builder.WithSettings(new WidgetsListPartSettings { Zones = (model.Zones ?? string.Empty).Split(s_separator, StringSplitOptions.RemoveEmptyEntries) });

        return Edit(contentTypePartDefinition, context);
    }
}
