using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentManagement.Display.Models;
using Grove.Contents.Models;
using Grove.Contents.ViewModels;
using Grove.DisplayManagement.Views;
using Grove.Modules;

namespace Grove.Contents.Drivers;

public sealed class DateEditorDriver : ContentPartDisplayDriver<CommonPart>
{
    private readonly ILocalClock _localClock;

    public DateEditorDriver(ILocalClock localClock)
    {
        _localClock = localClock;
    }

    public override IDisplayResult Edit(CommonPart part, BuildPartEditorContext context)
    {
        var settings = context.TypePartDefinition.GetSettings<CommonPartSettings>();

        if (settings.DisplayDateEditor)
        {
            return Initialize<DateEditorViewModel>("CommonPart_Edit__Date", async model =>
            {
                model.LocalDateTime = part.ContentItem.CreatedUtc.HasValue
                ? (await _localClock.ConvertToLocalAsync(part.ContentItem.CreatedUtc.Value)).DateTime
                : null;
            });
        }

        return null;
    }

    public override async Task<IDisplayResult> UpdateAsync(CommonPart part, UpdatePartEditorContext context)
    {
        var settings = context.TypePartDefinition.GetSettings<CommonPartSettings>();

        if (settings.DisplayDateEditor)
        {
            var model = new DateEditorViewModel();
            await context.Updater.TryUpdateModelAsync(model, Prefix);

            if (model.LocalDateTime == null)
            {
                part.ContentItem.CreatedUtc = null;
            }
            else
            {
                part.ContentItem.CreatedUtc = await _localClock.ConvertToUtcAsync(model.LocalDateTime.Value);
            }
        }

        return Edit(part, context);
    }
}
