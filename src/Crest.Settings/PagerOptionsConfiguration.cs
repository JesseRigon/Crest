using Microsoft.Extensions.Options;
using Crest.Navigation;

namespace Crest.Settings;

public class PagerOptionsConfiguration : IPostConfigureOptions<PagerOptions>
{
    private readonly ISiteService _siteService;

    public PagerOptionsConfiguration(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public void PostConfigure(string name, PagerOptions options)
    {
        var site = _siteService.GetSiteSettings();

        options.MaxPageSize = site.MaxPageSize;
        options.MaxPagedCount = site.MaxPagedCount;
        options.PageSize = site.PageSize;
        options.AllowPageSizeSelection = site.AllowPageSizeSelection;
        options.PageSizeOptions = site.PageSizeOptions;
    }
}
