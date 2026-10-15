using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Grove.Media.Library;
using Grove.Media.Models;
using Grove.ResourceManagement;
using Shortcodes;

namespace Grove.Media.Shortcodes;

public class AssetUrlShortcodeProvider : IShortcodeProvider
{
    private readonly IMediaReferenceResolver _references;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ResourceManagementOptions _options;

    public AssetUrlShortcodeProvider(
        IMediaReferenceResolver references,
        IHttpContextAccessor httpContextAccessor,
        IOptions<ResourceManagementOptions> options)
    {
        _references = references;
        _httpContextAccessor = httpContextAccessor;
        _options = options.Value;
    }

    public async ValueTask<string> EvaluateAsync(string identifier, Arguments arguments, string content, Context context)
    {
        if (!string.Equals(identifier, "asset_url", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // id="…" / path="…": the explicit reference forms (docs/shortcodes.md).
        var explicitUrl = await MediaShortcodeReference.ResolveExplicitAsync(arguments, _references);
        if (explicitUrl is not null)
        {
            content = explicitUrl;
        }

        // Handle self closing shortcodes.
        if (explicitUrl is null && string.IsNullOrEmpty(content))
        {
            content = arguments.NamedOrDefault("src");
            if (string.IsNullOrEmpty(content))
            {
                return "[asset_url]";
            }
        }

        if (explicitUrl is null && !content.StartsWith("//", StringComparison.Ordinal) && !content.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            // Serve static files from virtual path.
            if (content.StartsWith("~/", StringComparison.Ordinal))
            {
                content = _httpContextAccessor.HttpContext.Request.PathBase.Add(content[1..]).Value;
                if (!string.IsNullOrEmpty(_options.CdnBaseUrl))
                {
                    content = _options.CdnBaseUrl + content;
                }
            }
            else
            {
                // A placement id, a /files or /media URL, or a media library path (ruling F1).
                content = await _references.GetUrlAsync(content);
            }
        }

        if (arguments.Any())
        {
            var mediaCommands = new MediaCommands();

            var width = arguments.Named("width");
            var height = arguments.Named("height");
            var mode = arguments.Named("mode");
            var quality = arguments.Named("quality");
            var format = arguments.Named("format");
            var autoorient = arguments.Named("autoorient");

            if (width != null)
            {
                mediaCommands.Width = width;
            }

            if (height != null)
            {
                mediaCommands.Height = height;
            }

            if (mode != null)
            {
                mediaCommands.ResizeMode = mode;
            }

            if (quality != null)
            {
                mediaCommands.Quality = quality;
            }

            if (format != null)
            {
                mediaCommands.Format = format;
            }

            if (autoorient != null)
            {
                mediaCommands.AutoOrient = autoorient;
            }

            content = QueryHelpers.AddQueryString(content, mediaCommands.GetValues());
        }

        // This does not produce a tag, so sanitization is performed by the consumer (html body or markdown).

        return content;
    }
}
