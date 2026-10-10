using Microsoft.Extensions.Options;
using Crest.Settings;

namespace Crest.Media.Processing;

public sealed class MediaTokenOptionsConfiguration : IConfigureOptions<MediaTokenOptions>
{
    private readonly ISiteService _siteService;

    public MediaTokenOptionsConfiguration(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public void Configure(MediaTokenOptions options)
    {
        options.HashKey = _siteService.GetSettings<MediaTokenSettings>().HashKey;
    }
}
