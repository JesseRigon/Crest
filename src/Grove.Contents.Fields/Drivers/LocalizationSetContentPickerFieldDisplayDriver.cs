using Microsoft.Extensions.Localization;
using Grove.Contents.Fields.Fields;
using Grove.Contents.Fields.Settings;
using Grove.Contents.Fields.ViewModels;
using Grove.Contents.Localization;
using Grove.Contents.Management;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Contents.Management.Display.Models;
using Grove.Contents.Management.Metadata.Models;
using Grove.Display.Management.Views;
using Grove.Modules;

namespace Grove.Contents.Fields.Drivers;

[RequireFeatures("Grove.Contents.Localization")]
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
