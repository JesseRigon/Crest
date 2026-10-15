using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Grove.Media.Library;
using Grove.Infrastructure.Html;
using Grove.Media.Models;
using Grove.ResourceManagement;
using Shortcodes;

namespace Grove.Media.Shortcodes;

public class ImageShortcodeProvider : IShortcodeProvider
{
    private const string ImageShortcode = "[image]";
    private static readonly HashSet<string> s_shortcodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image",
        "media", // [media] is a deprecated shortcode, and can be removed in a future release.
    };

    private readonly IMediaReferenceResolver _references;
    private readonly IHtmlSanitizerService _htmlSanitizerService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ResourceManagementOptions _options;
    private readonly IFileVersionProvider _fileVersionProvider;

    public ImageShortcodeProvider(
        IMediaReferenceResolver references,
        IHtmlSanitizerService htmlSanitizerService,
        IHttpContextAccessor httpContextAccessor,
        IOptions<ResourceManagementOptions> options,
        IFileVersionProvider fileVersionProvider)
    {
        _references = references;
        _htmlSanitizerService = htmlSanitizerService;
        _httpContextAccessor = httpContextAccessor;
        _options = options.Value;
        _fileVersionProvider = fileVersionProvider;
    }

    public async ValueTask<string> EvaluateAsync(string identifier, Arguments arguments, string content, Context context)
    {
        if (!s_shortcodes.Contains(identifier))
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
                // Do not handle the deprecated media shortcode in this edge case.
                return ImageShortcode;
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
        var className = string.Empty;
        var altText = string.Empty;
        if (arguments.Any())
        {
            var mediaCommands = new MediaCommands();

            var width = arguments.Named("width");
            var height = arguments.Named("height");
            var mode = arguments.Named("mode");
            var quality = arguments.Named("quality");
            var format = arguments.Named("format");
            var appendVersion = arguments.Named("append_version");
            className = arguments.Named("class");
            altText = arguments.Named("alt");

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

            if (appendVersion?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false)
            {
                content = _fileVersionProvider.AddFileVersionToPath(_httpContextAccessor.HttpContext.Request.PathBase, content);
            }

            if (className != null)
            {
                className = "class=\"" + className + "\" ";
            }

            if (altText != null)
            {
                altText = "alt=\"" + altText + "\" ";
            }

            content = QueryHelpers.AddQueryString(content, mediaCommands.GetValues());
        }

        content = "<img " + altText + className + "src=\"" + content + "\">";
        content = _htmlSanitizerService.Sanitize(content);

        return content;
    }
}
