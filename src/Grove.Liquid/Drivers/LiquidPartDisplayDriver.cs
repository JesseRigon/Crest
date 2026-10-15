using Microsoft.Extensions.Localization;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentManagement.Display.Models;
using Grove.DisplayManagement.Views;
using Grove.Liquid.Models;
using Grove.Liquid.ViewModels;
using Grove.Mvc.ModelBinding;

namespace Grove.Liquid.Drivers;

public sealed class LiquidPartDisplayDriver : ContentPartDisplayDriver<LiquidPart>
{
    private readonly ILiquidTemplateManager _liquidTemplateManager;

    internal readonly IStringLocalizer S;

    public LiquidPartDisplayDriver(
        ILiquidTemplateManager liquidTemplateManager,
        IStringLocalizer<LiquidPartDisplayDriver> localizer)
    {
        _liquidTemplateManager = liquidTemplateManager;
        S = localizer;
    }

    public override Task<IDisplayResult> DisplayAsync(LiquidPart liquidPart, BuildPartDisplayContext context)
    {
        return CombineAsync(
            Initialize<LiquidPartViewModel>("LiquidPart", m => BuildViewModel(m, liquidPart))
                .Location(PlatformConstants.DisplayType.Detail, "Content"),
            Initialize<LiquidPartViewModel>("LiquidPart_Summary", m => BuildViewModel(m, liquidPart))
                .Location(PlatformConstants.DisplayType.Summary, "Content")
        );
    }

    public override IDisplayResult Edit(LiquidPart liquidPart, BuildPartEditorContext context)
    {
        return Initialize<LiquidPartViewModel>("LiquidPart_Edit", m => BuildViewModel(m, liquidPart));
    }

    public override async Task<IDisplayResult> UpdateAsync(LiquidPart model, UpdatePartEditorContext context)
    {
        var viewModel = new LiquidPartViewModel();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix, t => t.Liquid);

        if (!string.IsNullOrEmpty(viewModel.Liquid) && !_liquidTemplateManager.Validate(viewModel.Liquid, out var errors))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.Liquid), S["The Liquid Body doesn't contain a valid Liquid expression. Details: {0}", string.Join(" ", errors)]);
        }
        else
        {
            model.Liquid = viewModel.Liquid;
        }

        return Edit(model, context);
    }

    private static void BuildViewModel(LiquidPartViewModel model, LiquidPart liquidPart)
    {
        model.Liquid = liquidPart.Liquid;
        model.LiquidPart = liquidPart;
        model.ContentItem = liquidPart.ContentItem;
    }
}
