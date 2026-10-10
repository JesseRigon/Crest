using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Crest.Environment.Shell.Configuration;

namespace Crest.Media.Services;

public sealed class MediaSlugifyOptionsConfiguration : IConfigureOptions<MediaSlugifyOptions>
{
    private readonly IShellConfiguration _shellConfiguration;

    public MediaSlugifyOptionsConfiguration(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public void Configure(MediaSlugifyOptions options)
    {
        var section = _shellConfiguration.GetSection("Crest_Media_Slugify");

        options.Transliterate = section.GetValue(nameof(options.Transliterate), true);
    }
}
