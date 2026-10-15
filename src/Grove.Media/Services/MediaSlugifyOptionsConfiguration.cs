using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Grove.Environment.Shell.Configuration;

namespace Grove.Media.Services;

public sealed class MediaSlugifyOptionsConfiguration : IConfigureOptions<MediaSlugifyOptions>
{
    private readonly IShellConfiguration _shellConfiguration;

    public MediaSlugifyOptionsConfiguration(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public void Configure(MediaSlugifyOptions options)
    {
        var section = _shellConfiguration.GetSection("Grove_Media_Slugify");

        options.Transliterate = section.GetValue(nameof(options.Transliterate), true);
    }
}
