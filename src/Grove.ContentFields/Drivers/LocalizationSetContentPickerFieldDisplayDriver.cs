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
}
