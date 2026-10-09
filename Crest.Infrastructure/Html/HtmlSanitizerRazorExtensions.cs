using Microsoft.AspNetCore.Html;
using Microsoft.Extensions.DependencyInjection;
using Crest;
using Crest.Infrastructure.Html;

#pragma warning disable CA1050 // Declare types in namespaces
public static class HtmlSanitizerRazorExtensions
#pragma warning restore CA1050 // Declare types in namespaces
{
    /// <summary>
    /// Sanitizes a string of html.
    /// </summary>
    /// <param name="platformHelper">The <see cref="IPlatformHelper"/>.</param>
    /// <param name="html">The html to sanitize.</param>
    public static IHtmlContent SanitizeHtml(this IPlatformHelper platformHelper, string html)
    {
        var sanitizer = platformHelper.HttpContext.RequestServices.GetRequiredService<IHtmlSanitizerService>();
        html = sanitizer.Sanitize(html);

        return new HtmlString(html);
    }
}
