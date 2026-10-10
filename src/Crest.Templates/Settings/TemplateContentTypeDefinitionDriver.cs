using Microsoft.Extensions.Localization;
using Crest.ContentManagement.Metadata.Models;
using Crest.ContentTypes.Editors;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Templates.ViewModels;

namespace Crest.Templates.Settings;

public sealed class TemplateContentTypeDefinitionDriver : ContentTypeDefinitionDisplayDriver
{
    internal readonly IStringLocalizer S;

    public TemplateContentTypeDefinitionDriver(IStringLocalizer<TemplateContentTypeDefinitionDriver> localizer)
    {
        S = localizer;
    }

    public override IDisplayResult Edit(ContentTypeDefinition contentTypeDefinition, BuildEditorContext context)
    {
        return Initialize<ContentSettingsViewModel>("TemplateSettings", model =>
        {
            if (!contentTypeDefinition.TryGetStereotype(out var stereotype))
            {
                stereotype = "Content";
            }

            model.ContentSettingsEntries.Add(
                new ContentSettingsEntry
                {
                    Key = $"{stereotype}__{contentTypeDefinition.Name}",
                    Description = S["Template for a {0} content item in detail views", contentTypeDefinition.DisplayName],
                });

            model.ContentSettingsEntries.Add(
                new ContentSettingsEntry
                {
                    Key = $"{stereotype}_Summary__{contentTypeDefinition.Name}",
                    Description = S["Template for a {0} content item in summary views", contentTypeDefinition.DisplayName],
                });
        }).Location("Shortcuts");
    }
}
