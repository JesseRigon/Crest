using Microsoft.Extensions.Localization;
using Crest.Alias.Models;
using Crest.Alias.Settings;
using Crest.Alias.ViewModels;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Display.Models;
using Crest.DisplayManagement.Views;
using Crest.Mvc.ModelBinding;
using YesSql;

namespace Crest.Alias.Drivers;

public sealed class AliasPartDisplayDriver : ContentPartDisplayDriver<AliasPart>
{
    private readonly ISession _session;

    internal readonly IStringLocalizer S;

    public AliasPartDisplayDriver(
        ISession session,
        IStringLocalizer<AliasPartDisplayDriver> localizer
    )
    {
        _session = session;
        S = localizer;
    }

    public override IDisplayResult Edit(AliasPart aliasPart, BuildPartEditorContext context)
    {
        return Initialize<AliasPartViewModel>(GetEditorShapeType(context), m => BuildViewModel(m, aliasPart, context.TypePartDefinition.GetSettings<AliasPartSettings>()));
    }

    public override async Task<IDisplayResult> UpdateAsync(AliasPart model, UpdatePartEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, t => t.Alias);

        await foreach (var item in model.ValidateAsync(S, _session))
        {
            context.Updater.ModelState.BindValidationResult(Prefix, item);
        }

        return Edit(model, context);
    }

    private static void BuildViewModel(AliasPartViewModel model, AliasPart part, AliasPartSettings settings)
    {
        model.Alias = part.Alias;
        model.AliasPart = part;
        model.ContentItem = part.ContentItem;
        model.Settings = settings;
    }
}
