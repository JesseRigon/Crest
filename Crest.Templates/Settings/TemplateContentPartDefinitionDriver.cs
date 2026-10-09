using Microsoft.Extensions.Localization;
using Crest.ContentManagement.Metadata.Models;
using Crest.ContentManagement.Metadata.Settings;
using Crest.ContentTypes.Editors;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Templates.ViewModels;

namespace Crest.Templates.Settings;

public sealed class TemplateContentPartDefinitionDriver : ContentPartDefinitionDisplayDriver
{
    internal readonly IStringLocalizer S;

    public TemplateContentPartDefinitionDriver(IStringLocalizer<TemplateContentPartDefinitionDriver> localizer)
    {
        S = localizer;
    }

    public override IDisplayResult Edit(ContentPartDefinition contentPartDefinition, BuildEditorContext context)
    {
        return Initialize<ContentSettingsViewModel>("TemplateSettings", model =>
        {
            model.ContentSettingsEntries.Add(
                new ContentSettingsEntry
                {
                    Key = contentPartDefinition.Name,
                    Description = S["Template for a {0} part in detail views", contentPartDefinition.DisplayName()],
                });

            model.ContentSettingsEntries.Add(
                new ContentSettingsEntry
                {
                    Key = $"{contentPartDefinition.Name}_Summary",
                    Description = S["Template for a {0} part in summary views", contentPartDefinition.DisplayName()],
                });
        }).Location("Shortcuts");
    }
}
