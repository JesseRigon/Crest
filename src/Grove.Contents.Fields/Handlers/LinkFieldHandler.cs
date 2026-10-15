using Grove.Routing;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Grove.Contents.Fields.Fields;
using Grove.Contents.Fields.Settings;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Metadata.Models;
using Grove.Display.Management.Extensions;
using Grove.Infrastructure.Html;

namespace Grove.Contents.Fields.Handlers;

public class LinkFieldHandler : ContentFieldHandler<LinkField>
{
    private readonly IShellLinkGenerator _links;
    private readonly IHttpContextAccessor _httpContextAccessor;
    protected readonly IStringLocalizer S;
    private readonly IHtmlSanitizerService _htmlSanitizerService;
    private readonly HtmlEncoder _htmlencoder;

    public LinkFieldHandler(
        IShellLinkGenerator links,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<LinkFieldHandler> localizer,
        IHtmlSanitizerService htmlSanitizerService,
        HtmlEncoder htmlencoder)
    {
        _links = links;
        _httpContextAccessor = httpContextAccessor;
        S = localizer;
        _htmlSanitizerService = htmlSanitizerService;
        _htmlencoder = htmlencoder;
    }

    public override Task ValidatingAsync(ValidateContentFieldContext context, LinkField field)
    {
        var settings = context.ContentPartFieldDefinition.GetSettings<LinkFieldSettings>();

        var urlToValidate = field.Url;
        if (!string.IsNullOrEmpty(urlToValidate))
        {
            var indexAnchor = urlToValidate.IndexOf('#');
            if (indexAnchor > -1)
            {
                urlToValidate = urlToValidate[..indexAnchor];
            }

            if (urlToValidate.StartsWith("~/", StringComparison.Ordinal))
            {
                urlToValidate = _links.Content(urlToValidate);
            }

            urlToValidate = urlToValidate.ToUriComponents();
        }

        // Validate Url
        if (settings.Required && string.IsNullOrWhiteSpace(field.Url))
        {
            context.Fail(S["The url is required for {0}.", context.ContentPartFieldDefinition.DisplayName()], nameof(field.Url));
        }
        else if (!string.IsNullOrWhiteSpace(field.Url))
        {
            if (!Uri.IsWellFormedUriString(urlToValidate, UriKind.RelativeOrAbsolute))
            {
                context.Fail(S["{0} is an invalid url.", field.Url], nameof(field.Url));
            }
            else
            {
                var link = $"<a href=\"{_htmlencoder.Encode(urlToValidate)}\"></a>";

                if (!string.Equals(link, _htmlSanitizerService.Sanitize(link), StringComparison.OrdinalIgnoreCase))
                {
                    context.Fail(S["{0} is an invalid url.", field.Url], nameof(field.Url));
                }
            }
        }

        if (settings.LinkTextMode == LinkTextMode.Required && string.IsNullOrWhiteSpace(field.Text))
        {
            context.Fail(S["The link text is required for {0}.", context.ContentPartFieldDefinition.DisplayName()], nameof(field.Text));
        }
        else if (settings.LinkTextMode == LinkTextMode.Static && string.IsNullOrWhiteSpace(settings.DefaultText))
        {
            context.Fail(S["The text default value is required for {0}.", context.ContentPartFieldDefinition.DisplayName()], nameof(field.Text));
        }

        return Task.CompletedTask;
    }
}
