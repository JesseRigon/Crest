using Crest.Infrastructure.Html;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class PlatformBuilderExtensions
{
    /// <summary>
    /// Adds html script sanitization services.
    /// </summary>
    /// <param name="builder">The <see cref="PlatformBuilder"/>.</param>
    public static PlatformBuilder AddHtmlSanitizer(this PlatformBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddOptions<HtmlSanitizerOptions>();

            services.ConfigureHtmlSanitizer((sanitizer) =>
            {
                sanitizer.AllowedAttributes.Add("class");
                sanitizer.AllowedTags.Remove("form");

                sanitizer.AllowedSchemes.Add("mailto");
                sanitizer.AllowedSchemes.Add("tel");
            });

            services.AddSingleton<IHtmlSanitizerService, HtmlSanitizerService>();
        });

        return builder;
    }
}
