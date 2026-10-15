using System.Text.Encodings.Web;
using Grove.Display.Management.Components;
using Grove.Display.Management.Document;
using Grove.Integrations.Google.Analytics.Settings;
using Grove.Infrastructure.Settings;

namespace Grove.Integrations.Google.Analytics;

/// <summary>The Google Analytics tag in the site's documents, when the visitor allows tracking.</summary>
public sealed class GoogleAnalyticsDocumentContributor(ISiteService siteService, JavaScriptEncoder jsEncoder, UrlEncoder urlEncoder) : IDocumentContributor
{
    public async ValueTask ContributeAsync(DocumentContext context)
    {
        if (context.Shell != ShellKind.Site || !context.CanTrack)
        {
            return;
        }

        var settings = await siteService.GetSettingsAsync<GoogleAnalyticsSettings>();
        if (string.IsNullOrEmpty(settings?.TrackingID))
        {
            return;
        }

        context.Add(DocumentSlot.Head,
            "<!-- Global site tag (gtag.js) - Google Analytics -->\n<script async src=\"https://www.googletagmanager.com/gtag/js?id="
            + urlEncoder.Encode(settings.TrackingID)
            + "\"></script>\n<script>window.dataLayer = window.dataLayer || [];function gtag() { dataLayer.push(arguments); }gtag('js', new Date());gtag('config', '"
            + jsEncoder.Encode(settings.TrackingID)
            + "')</script>\n<!-- End Global site tag (gtag.js) - Google Analytics -->");
    }
}
