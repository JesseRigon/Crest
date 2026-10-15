using Microsoft.Extensions.Localization;
using Grove.ContentFields.Fields;
using Grove.ContentFields.Settings;
using Grove.ContentFields.ViewModels;
using Grove.ContentLocalization;
using Grove.ContentManagement;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentManagement.Display.Models;
using Grove.ContentManagement.Metadata.Models;
using Grove.DisplayManagement.Views;
using Grove.Modules;
using Grove.Mvc.ModelBinding;

namespace Grove.ContentFields.Drivers;

[RequireFeatures("Grove.ContentLocalization")]
public sealed class LocalizationSetContentPickerFieldDisplayDriver : ContentFieldDisplayDriver<LocalizationSetContentPickerField>
{
    private readonly IContentManager _contentManager;
    private readonly IContentLocalizationManager _contentLocalizationManager;

    internal readonly IStringLocalizer S;

    public LocalizationSetContentPickerFieldDisplayDriver(
        IContentManager contentManager,
        IStringLocalizer<LocalizationSetContentPickerFieldDisplayDriver> localizer,
        IContentLocalizationManager contentLocalizationManager)
    {
        _contentManager = contentManager;
        S = localizer;
        _contentLocalizationManager = contentLocalizationManager;
    }

    public override IDisplayResult Display(LocalizationSetContentPickerField field, BuildFieldDisplayContext context)
    {
        return Initialize<DisplayLocalizationSetContentPickerFieldViewModel>(GetDisplayShapeType(context), async model =>
        {
            model.Field = field;
            model.Part = context.ContentPart;
            model.PartFieldDefinition = context.PartFieldDefinition;
            var items = await _contentLocalizationManager.GetItemsForSetsAsync(field.LocalizationSets ?? [], System.Globalization.CultureInfo.CurrentUICulture.Name);
            model.Items = items.Select(item => new DisplayLinkItem(item.DisplayText ?? item.ContentItemId, ContentItemLinks.SitePath(item))).ToArray();
        })
        .Location(PlatformConstants.DisplayType.Detail, "Content")
        .Location(PlatformConstants.DisplayType.Summary, "Content");
    }

    public override IDisplayResult Edit(LocalizationSetContentPickerField field, BuildFieldEditorContext context)
    {
        return Initialize<EditLocalizationSetContentPickerFieldViewModel>(GetEditorShapeType(context), async model =>
        {
            model.LocalizationSets = string.Join(",", field.LocalizationSets);

            model.Field = field;
            model.Part = context.ContentPart;
            model.PartFieldDefinition = context.PartFieldDefinition;

            model.SelectedItems = [];

            foreach (var kvp in await _contentLocalizationManager.GetFirstItemIdForSetsAsync(field.LocalizationSets))
            {
                var contentItem = await _contentManager.GetAsync(kvp.Value, VersionOptions.Latest);

                if (contentItem == null)
                {
                    continue;
                }

                model.SelectedItems.Add(new VueMultiselectItemViewModel
                {
                    Id = kvp.Key, // localization set
                    DisplayText = contentItem.ToString(),
                    HasPublished = await _contentManager.HasPublishedVersionAsync(contentItem),
                });
            }
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(LocalizationSetContentPickerField field, UpdateFieldEditorContext context)
    {
        var viewModel = new EditLocalizationSetContentPickerFieldViewModel();

        var modelUpdated = await context.Updater.TryUpdateModelAsync(viewModel, Prefix, f => f.LocalizationSets);

        if (!modelUpdated)
        {
            return Edit(field, context);
        }

        field.LocalizationSets = viewModel.LocalizationSets == null
            ? [] : viewModel.LocalizationSets.Split(',', StringSplitOptions.RemoveEmptyEntries);

        var settings = context.PartFieldDefinition.GetSettings<LocalizationSetContentPickerFieldSettings>();

        if (settings.Required && field.LocalizationSets.Length == 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(field.LocalizationSets), S["The {0} field is required.", context.PartFieldDefinition.DisplayName()]);
        }

        if (!settings.Multiple && field.LocalizationSets.Length > 1)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(field.LocalizationSets), S["The {0} field cannot contain multiple items.", context.PartFieldDefinition.DisplayName()]);
        }

        return Edit(field, context);
    }
}
