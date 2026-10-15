using System.Text.Encodings.Web;
using Grove.Display.Management.Components;
using Grove.Display.Management.Document;
using Grove.Integrations.Google.TagManager.Settings;
using Grove.Infrastructure.Settings;

namespace Grove.Integrations.Google.TagManager;

/// <summary>The Google Tag Manager snippet in the site's documents, when the visitor allows tracking.</summary>
public sealed class GoogleTagManagerDocumentContributor(ISiteService siteService, JavaScriptEncoder jsEncoder) : IDocumentContributor
{
    public async ValueTask ContributeAsync(DocumentContext context)
    {
        if (context.Shell != ShellKind.Site || !context.CanTrack)
        {
            return;
        }

        var settings = await siteService.GetSettingsAsync<GoogleTagManagerSettings>();
        if (string.IsNullOrEmpty(settings?.ContainerID))
        {
            return;
        }

        context.Add(DocumentSlot.Head,
            "<!-- Google Tag Manager -->\n<script>(function(w,d,s,l,i){w[l]=w[l]||[];w[l].push({'gtm.start':\n  new Date().getTime(),event:'gtm.js'});var f=d.getElementsByTagName(s)[0],\n  j=d.createElement(s),dl=l!='dataLayer'?'&l='+l:'';j.async=true;j.src=\n  'https://www.googletagmanager.com/gtm.js?id='+i+dl;f.parentNode.insertBefore(j,f);\n  })(window,document,'script','dataLayer','"
            + jsEncoder.Encode(settings.ContainerID)
            + "');</script>\n<!-- End Google Tag Manager -->");
    }
}
