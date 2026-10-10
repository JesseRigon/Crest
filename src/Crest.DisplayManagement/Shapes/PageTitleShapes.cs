using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;
using Microsoft.Extensions.DependencyInjection;
using Crest.DisplayManagement.Descriptors;
using Crest.DisplayManagement.Title;
using Crest.Environment.Shell.Scope;
using Crest.Liquid;
using Crest.Modules;
using Crest.Settings;

namespace Crest.DisplayManagement.Shapes;

[Feature(Application.DefaultFeatureId)]
public class PageTitleShapes : IShapeAttributeProvider
{
    private IPageTitleBuilder _pageTitleBuilder;

    public IPageTitleBuilder Title => _pageTitleBuilder ??= ShellScope.Services.GetRequiredService<IPageTitleBuilder>();

    [Shape]
    public async Task<IHtmlContent> PageTitle()
    {
        var siteSettings = await ShellScope.Services.GetRequiredService<ISiteService>().GetSiteSettingsAsync();

        // We must return a page title so if the format setting is blank just use the current title unformatted
        if (string.IsNullOrWhiteSpace(siteSettings.PageTitleFormat))
        {
            return Title.GenerateTitle(null);
        }
        else
        {
            var liquidTemplateManager = ShellScope.Services.GetRequiredService<ILiquidTemplateManager>();
            var htmlEncoder = ShellScope.Services.GetRequiredService<HtmlEncoder>();

            var result = await liquidTemplateManager.RenderHtmlContentAsync(siteSettings.PageTitleFormat, htmlEncoder);
            return result;
        }
    }
}
