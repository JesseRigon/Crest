using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;
using Microsoft.Extensions.DependencyInjection;
using Crest;
using Crest.Liquid;

#pragma warning disable CA1050 // Declare types in namespaces
public static class LiquidRazorHelperExtensions
#pragma warning restore CA1050 // Declare types in namespaces
{
    /// <summary>
    /// Parses a liquid string to HTML.
    /// </summary>
    /// <param name="platformHelper">The <see cref="IPlatformHelper"/>.</param>
    /// <param name="liquid"></param>
    public static Task<IHtmlContent> LiquidToHtmlAsync(this IPlatformHelper platformHelper, string liquid)
    {
        return platformHelper.LiquidToHtmlAsync(liquid, null);
    }

    /// <summary>
    /// Parses a liquid string to HTML.
    /// </summary>
    /// <param name="platformHelper">The <see cref="IPlatformHelper"/>.</param>
    /// <param name="liquid">The liquid to parse.</param>
    /// <param name="model">A model to bind against.</param>
    public static async Task<IHtmlContent> LiquidToHtmlAsync(this IPlatformHelper platformHelper, string liquid, object model)
    {
        var serviceProvider = platformHelper.HttpContext.RequestServices;

        var liquidTemplateManager = serviceProvider.GetRequiredService<ILiquidTemplateManager>();
        var htmlEncoder = serviceProvider.GetRequiredService<HtmlEncoder>();

        var result = await liquidTemplateManager.RenderHtmlContentAsync(liquid, htmlEncoder, model);
        return result;
    }
}
