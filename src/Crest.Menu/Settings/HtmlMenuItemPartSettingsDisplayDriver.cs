using Crest.ContentManagement.Metadata.Models;
using Crest.ContentTypes.Editors;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Menu.Models;
using Crest.Menu.ViewModels;

namespace Crest.Menu.Settings;

public sealed class HtmlMenuItemPartSettingsDisplayDriver : ContentTypePartDefinitionDisplayDriver<HtmlMenuItemPart>
{
    public override IDisplayResult Edit(ContentTypePartDefinition contentTypePartDefinition, BuildEditorContext context)
    {
        return Initialize<HtmlMenuItemPartSettingsViewModel>("HtmlMenuItemPartSettings_Edit", model =>
        {
            var settings = contentTypePartDefinition.GetSettings<HtmlMenuItemPartSettings>();

            model.SanitizeHtml = settings.SanitizeHtml;
        }).Location("Content:20");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentTypePartDefinition contentTypePartDefinition, UpdateTypePartEditorContext context)
    {
        var model = new HtmlMenuItemPartSettingsViewModel();
        var settings = new HtmlMenuItemPartSettings();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.SanitizeHtml = model.SanitizeHtml;

        context.Builder.WithSettings(settings);

        return Edit(contentTypePartDefinition, context);
    }
}
