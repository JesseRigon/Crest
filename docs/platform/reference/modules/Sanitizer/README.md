# HTML Sanitizer

An HTML Sanitizer is available as part of the Crest Infrastructure.

The Sanitizer cleans user input that could lead to XSS attacks.

It is used by default for the following parts and fields:

- HTML Body Part
- HTML Field
- HTML Menu Item Part
- Markdown Body Part
- Markdown Field

!!! note
    To disable sanitization on these fields disable the `Sanitize Html` option in the field or part settings.

## Razor Helper

`@Platform.SanitizeHtml((string)Model.ContentItem.HtmlBodyPart.Html);`

## Defaults configuration

The elements sanitized by default are listed on this page: <https://github.com/mganss/HtmlSanitizer#tags-allowed-by-default>

Crest changes these defaults by:

- allowing the attribute `class`
- allowing the `mailto` and `tel` schemes
- removing the tag `form`

## Configuring the Sanitizer

The sanitizer is configurable using `IOptions<HtmlSanitizerOptions>` during service registration with a configuration
extension method `ConfigureHtmlSanitizer`.

You may call this extension method multiple times during the startup pipeline to alter configurations.

```csharp
services
    .AddPlatformCms()
    .ConfigureServices(tenantServices =>
        tenantServices.ConfigureHtmlSanitizer((sanitizer) =>
            {
                sanitizer.AllowedSchemes.Add("ftp");
            }));
```

Refer <https://github.com/mganss/HtmlSanitizer> for options.
