using System.Globalization;
using System.Text.Encodings.Web;
using Grove.Display.Management.Components;
using Grove.Display.Management.Document;
using Grove.Integrations.Facebook.Endpoints;
using Grove.Integrations.Facebook.Services;
using Grove.Integrations.Facebook.Settings;
using Grove.Infrastructure.Settings;

namespace Grove.Integrations.Facebook.Filters;

/// <summary>
/// The Facebook SDK and its init script at the end of the site's documents, when the Facebook
/// app is configured with <c>FBInit</c>: the SDK in the request's culture (or en-US), then the init.
/// </summary>
public sealed class FacebookSdkDocumentContributor(ISiteService siteService, IFacebookService facebookService, HtmlEncoder htmlEncoder) : IDocumentContributor
{
    public async ValueTask ContributeAsync(DocumentContext context)
    {
        if (context.Shell != ShellKind.Site)
        {
            return;
        }

        var site = await siteService.GetSiteSettingsAsync();
        if (!site.TryGet<FacebookSettings>(out var settings) || !settings.FBInit)
        {
            return;
        }

        var appId = await facebookService.GetAppIdAsync();
        if (string.IsNullOrWhiteSpace(appId))
        {
            return;
        }

        var culture = CultureInfo.CurrentUICulture.Name;
        if (!GetSdkEndpoints.GetFetchScriptEndpoint.ValidFacebookCultures.Contains(culture, StringComparer.OrdinalIgnoreCase))
        {
            culture = "en-US";
        }

        var root = context.HttpContext.Request.PathBase.Value?.TrimEnd('/') ?? string.Empty;
        var version = GetSdkEndpoints.GetInitScriptEndpoint.HashCacheBustingValues(settings, appId);

        context.Add(DocumentSlot.BodyEnd,
            $"<script src=\"{htmlEncoder.Encode($"{root}/Grove.Integrations.Facebook/sdk/sdk.{culture}.js?v={version}")}\"></script>\n"
            + $"<script src=\"{htmlEncoder.Encode($"{root}/Grove.Integrations.Facebook/sdk/init.js?v={version}")}\"></script>");
    }
}
