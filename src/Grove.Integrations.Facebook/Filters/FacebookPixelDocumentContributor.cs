using System.Text.Encodings.Web;
using Grove.Display.Management.Components;
using Grove.Display.Management.Document;
using Grove.Integrations.Facebook.Settings;
using Grove.Infrastructure.Settings;

namespace Grove.Integrations.Facebook.Filters;

/// <summary>The Meta Pixel snippet in the site's documents, when the visitor allows tracking.</summary>
public sealed class FacebookPixelDocumentContributor(ISiteService siteService, JavaScriptEncoder jsEncoder) : IDocumentContributor
{
    public async ValueTask ContributeAsync(DocumentContext context)
    {
        if (context.Shell != ShellKind.Site || !context.CanTrack)
        {
            return;
        }

        var settings = await siteService.GetSettingsAsync<FacebookPixelSettings>();
        if (string.IsNullOrEmpty(settings?.PixelId))
        {
            return;
        }

        context.Add(DocumentSlot.Head,
            "<!-- Meta Pixel Code -->\r\n<script>\r\n!function(f,b,e,v,n,t,s)\r\n{if(f.fbq)return;n=f.fbq=function(){n.callMethod?\r\nn.callMethod.apply(n,arguments):n.queue.push(arguments)};\r\nif(!f._fbq)f._fbq=n;n.push=n;n.loaded=!0;n.version='2.0';\r\nn.queue=[];t=b.createElement(e);t.async=!0;\r\nt.src=v;s=b.getElementsByTagName(e)[0];\r\ns.parentNode.insertBefore(t,s)}(window, document,'script',\r\n'https://connect.facebook.net/en_US/fbevents.js');\r\nfbq('init', '"
            + jsEncoder.Encode(settings.PixelId)
            + "');\r\nfbq('track', 'PageView');\r\n</script>\r\n<!-- End Meta Pixel Code -->");
    }
}
