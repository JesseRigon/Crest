using Microsoft.Extensions.Localization;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Display.Models;
using Crest.ContentManagement.Metadata;
using Crest.DisplayManagement.Views;
using Crest.Facebook.Widgets.Models;
using Crest.Facebook.Widgets.Settings;
using Crest.Facebook.Widgets.ViewModels;
using Crest.Liquid;

namespace Crest.Facebook.Widgets.Drivers;

public sealed class FacebookPluginPartDisplayDriver : ContentPartDisplayDriver<FacebookPluginPart>
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ILiquidTemplateManager _liquidTemplateManager;

    internal readonly IStringLocalizer S;

    public FacebookPluginPartDisplayDriver(
        IContentDefinitionManager contentDefinitionManager,
        ILiquidTemplateManager liquidTemplateManager,
        IStringLocalizer<FacebookPluginPartDisplayDriver> localizer)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _liquidTemplateManager = liquidTemplateManager;
        S = localizer;
    }

    public override IDisplayResult Display(FacebookPluginPart part, BuildPartDisplayContext context)
    {
        return Combine(
            Initialize<FacebookPluginPartViewModel>("FacebookPluginPart", async m => await BuildViewModelAsync(m, part))
                .Location(PlatformConstants.DisplayType.Detail, "Content"),
            Initialize<FacebookPluginPartViewModel>("FacebookPluginPart_Summary", async m => await BuildViewModelAsync(m, part))
                .Location(PlatformConstants.DisplayType.Summary, "Content")
        );
    }

    private async Task BuildViewModelAsync(FacebookPluginPartViewModel model, FacebookPluginPart part)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(part);

        model.FacebookPluginPart = part;
        model.Settings = await GetFacebookPluginPartSettingsAsync(part);
        model.Liquid = part.Liquid;
        model.ContentItem = part.ContentItem;
    }

    public override IDisplayResult Edit(FacebookPluginPart part, BuildPartEditorContext context)
    {
        return Initialize<FacebookPluginPartViewModel>("FacebookPluginPart_Edit", async model =>
        {
            model.Settings = await GetFacebookPluginPartSettingsAsync(part);
            model.FacebookPluginPart = part;
            model.Liquid = string.IsNullOrWhiteSpace(part.Liquid) ? model.Settings.Liquid : part.Liquid;
        });
    }

    private async Task<FacebookPluginPartSettings> GetFacebookPluginPartSettingsAsync(FacebookPluginPart part)
    {
        ArgumentNullException.ThrowIfNull(part);

        var contentTypeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(part.ContentItem.ContentType);
        var contentTypePartDefinition = contentTypeDefinition.Parts.FirstOrDefault(x => string.Equals(x.PartDefinition.Name, nameof(FacebookPluginPart), StringComparison.Ordinal));
        return contentTypePartDefinition.GetSettings<FacebookPluginPartSettings>();
    }

    public override async Task<IDisplayResult> UpdateAsync(FacebookPluginPart model, UpdatePartEditorContext context)
    {
        var viewModel = new FacebookPluginPartViewModel();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix, t => t.Liquid);

        if (!string.IsNullOrEmpty(viewModel.Liquid) && !_liquidTemplateManager.Validate(viewModel.Liquid, out var errors))
        {
            context.Updater.ModelState.AddModelError(nameof(model.Liquid), S["The FaceBook Body doesn't contain a valid Liquid expression. Details: {0}", string.Join(" ", errors)]);
        }
        else
        {
            model.Liquid = viewModel.Liquid;
        }

        return Edit(model, context);
    }
}
