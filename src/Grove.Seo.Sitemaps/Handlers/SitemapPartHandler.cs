using Grove.Contents.Management.Handlers;
using Grove.Seo.Sitemaps.Aspects;
using Grove.Seo.Sitemaps.Models;

namespace Grove.Seo.Sitemaps.Handlers;

public class SitemapPartHandler : ContentPartHandler<SitemapPart>
{
    public override Task GetContentItemAspectAsync(ContentItemAspectContext context, SitemapPart part)
    {
        return context.ForAsync<SitemapMetadataAspect>(aspect =>
        {
            if (part.OverrideSitemapConfig)
            {
                aspect.ChangeFrequency = part.ChangeFrequency.ToString();
                aspect.Priority = part.Priority;
                aspect.Exclude = part.Exclude;
            }

            return Task.CompletedTask;
        });
    }
}
