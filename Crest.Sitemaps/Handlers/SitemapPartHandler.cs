using Crest.ContentManagement.Handlers;
using Crest.Sitemaps.Aspects;
using Crest.Sitemaps.Models;

namespace Crest.Sitemaps.Handlers;

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
