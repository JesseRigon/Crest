using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentManagement.Display.Models;
using Grove.DisplayManagement.Views;
using Grove.Sitemaps.Models;
using Grove.Sitemaps.ViewModels;

namespace Grove.Sitemaps.Drivers;

public sealed class SitemapPartDisplayDriver : ContentPartDisplayDriver<SitemapPart>
{
    public override IDisplayResult Edit(SitemapPart part, BuildPartEditorContext context)
    {
        return Initialize<SitemapPartViewModel>("SitemapPart_Edit", m => BuildViewModel(m, part))
            .Location("Parts#SEO:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(SitemapPart model, UpdatePartEditorContext context)
    {
        var viewModel = new SitemapPartViewModel();

        await context.Updater.TryUpdateModelAsync(viewModel,
            Prefix,
            t => t.OverrideSitemapConfig,
            t => t.ChangeFrequency,
            t => t.Exclude,
            t => t.Priority);

        model.OverrideSitemapConfig = viewModel.OverrideSitemapConfig;
        model.ChangeFrequency = viewModel.ChangeFrequency;
        model.Exclude = viewModel.Exclude;
        model.Priority = viewModel.Priority;

        return Edit(model, context);
    }


    private static void BuildViewModel(SitemapPartViewModel model, SitemapPart part)
    {
        model.OverrideSitemapConfig = part.OverrideSitemapConfig;
        model.ChangeFrequency = part.ChangeFrequency;
        model.Exclude = part.Exclude;
        model.Priority = part.Priority;
        model.SitemapPart = part;
    }
}
