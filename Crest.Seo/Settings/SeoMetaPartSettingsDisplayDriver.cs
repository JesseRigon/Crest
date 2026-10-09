using Crest.ContentManagement.Metadata.Models;
using Crest.ContentTypes.Editors;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Seo.Models;
using Crest.Seo.ViewModels;

namespace Crest.SeoMeta.Settings;

public sealed class SeoMetaPartSettingsDisplayDriver : ContentTypePartDefinitionDisplayDriver<SeoMetaPart>
{
    public override IDisplayResult Edit(ContentTypePartDefinition contentTypePartDefinition, BuildEditorContext context)
    {
        return Initialize<SeoMetaPartSettingsViewModel>("SeoMetaPartSettings_Edit", model =>
        {
            var settings = contentTypePartDefinition.GetSettings<SeoMetaPartSettings>();

            model.DisplayKeywords = settings.DisplayKeywords;
            model.DisplayCustomMetaTags = settings.DisplayCustomMetaTags;
            model.DisplayOpenGraph = settings.DisplayOpenGraph;
            model.DisplayTwitter = settings.DisplayTwitter;
            model.DisplayGoogleSchema = settings.DisplayGoogleSchema;
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentTypePartDefinition contentTypePartDefinition, UpdateTypePartEditorContext context)
    {
        var model = new SeoMetaPartSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix,
            m => m.DisplayKeywords,
            m => m.DisplayCustomMetaTags,
            m => m.DisplayOpenGraph,
            m => m.DisplayTwitter,
            m => m.DisplayGoogleSchema);

        context.Builder.WithSettings(new SeoMetaPartSettings
        {
            DisplayKeywords = model.DisplayKeywords,
            DisplayCustomMetaTags = model.DisplayCustomMetaTags,
            DisplayOpenGraph = model.DisplayOpenGraph,
            DisplayTwitter = model.DisplayTwitter,
            DisplayGoogleSchema = model.DisplayGoogleSchema,
        });

        return Edit(contentTypePartDefinition, context);
    }
}
