using Microsoft.Extensions.Localization;
using Crest.ContentManagement.Metadata.Models;
using Crest.ContentTypes.Editors;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Templates.ViewModels;

namespace Crest.Templates.Settings;

public sealed class TemplateContentTypePartDefinitionDriver : ContentTypePartDefinitionDisplayDriver
{
    internal readonly IStringLocalizer S;

    public TemplateContentTypePartDefinitionDriver(IStringLocalizer<TemplateContentTypePartDefinitionDriver> localizer)
    {
        S = localizer;
    }

    public override IDisplayResult Edit(ContentTypePartDefinition contentTypePartDefinition, BuildEditorContext context)
    {
        return Initialize<ContentSettingsViewModel>("TemplateSettings", model =>
        {
            var contentType = contentTypePartDefinition.ContentTypeDefinition.Name;
            var partName = contentTypePartDefinition.Name;

            model.ContentSettingsEntries.Add(
                new ContentSettingsEntry
                {
                    Key = $"{contentType}__{partName}",
                    Description = S["Template for the {0} part in a {1} type in detail views", partName, contentTypePartDefinition.ContentTypeDefinition.DisplayName],
                });

            model.ContentSettingsEntries.Add(
                new ContentSettingsEntry
                {
                    Key = $"{contentType}_Summary__{partName}",
                    Description = S["Template for the {0} part in a {1} type in summary views", partName, contentTypePartDefinition.ContentTypeDefinition.DisplayName],
                });
        }).Location("Shortcuts");
    }
}
