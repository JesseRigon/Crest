using Microsoft.Extensions.Localization;
using Crest.ContentManagement.Metadata.Models;
using Crest.ContentManagement.Metadata.Settings;
using Crest.ContentTypes.Editors;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Placements.ViewModels;

namespace Crest.Placements.Settings;

public sealed class PlacementContentPartDefinitionDriver : ContentPartDefinitionDisplayDriver
{
    internal readonly IStringLocalizer S;

    public PlacementContentPartDefinitionDriver(IStringLocalizer<PlacementContentPartDefinitionDriver> localizer)
    {
        S = localizer;
    }

    public override IDisplayResult Edit(ContentPartDefinition contentPartDefinition, BuildEditorContext context)
    {
        var displayName = contentPartDefinition.DisplayName();

        return Initialize<ContentSettingsViewModel>("PlacementSettings", model =>
        {
            model.ContentSettingsEntries.Add(
                new ContentSettingsEntry
                {
                    ShapeType = contentPartDefinition.Name,
                    Description = S["Placement for a {0} part", displayName],
                });

            model.ContentSettingsEntries.Add(
                new ContentSettingsEntry
                {
                    ShapeType = contentPartDefinition.Name,
                    DisplayType = "Detail",
                    Description = S["Placement for a {0} part in detail views", displayName],
                });

            model.ContentSettingsEntries.Add(
                new ContentSettingsEntry
                {
                    ShapeType = contentPartDefinition.Name,
                    DisplayType = "Summary",
                    Description = S["Placement for a {0} part in summary views", displayName],
                });

            model.ContentSettingsEntries.Add(
                new ContentSettingsEntry
                {
                    ShapeType = $"{contentPartDefinition.Name}_Edit",
                    Description = S["Placement in admin editor for a {0} part", displayName],
                });

        }).Location("Shortcuts");
    }
}
