using Grove.Contents.Management.Handlers;
using Grove.Seo.Sitemaps.Aspects;
using Grove.Seo.Sitemaps.Models;

namespace Grove.Seo.Sitemaps.Projections;

/// <summary>An item's sitemap entry (a read projection): its overrides of the sitemap's frequency, priority and exclusion.</summary>
public sealed class SitemapProjection : ContentFieldSetProjection<SitemapFieldSet>
{
    protected override Task ProjectAsync(ContentItemAspectContext context, SitemapFieldSet part)
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
