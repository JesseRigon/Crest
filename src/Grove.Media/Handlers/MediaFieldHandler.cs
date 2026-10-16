using Microsoft.Extensions.Localization;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Metadata.Models;
using Grove.Media.Fields;
using Grove.Media.Settings;

namespace Grove.Media.Handlers;

/// <summary>A media field's rules on every save path: required, one item unless multiple, the allowed extensions.</summary>
public class MediaFieldHandler : ContentFieldHandler<MediaField>
{
    protected readonly IStringLocalizer S;

    public MediaFieldHandler(IStringLocalizer<MediaFieldHandler> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task ValidatingAsync(ValidateContentFieldContext context, MediaField field)
    {
        var settings = context.ContentFieldDefinition.GetSettings<MediaFieldSettings>();
        var paths = field.Paths ?? [];
        var name = context.ContentFieldDefinition.DisplayName();

        if (settings.Required && paths.Length < 1)
        {
            context.Fail(S["A value is required for {0}.", name], nameof(field.Paths));
        }

        if (paths.Length > 1 && !settings.Multiple)
        {
            context.Fail(S["{0}: Selecting multiple media is forbidden.", name], nameof(field.Paths));
        }

        if (settings.AllowedExtensions is { Length: > 0 } allowed
            && paths.Any(path => !string.IsNullOrEmpty(path) && !allowed.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)))
        {
            context.Fail(S["Media extension is not allowed. Only media with '{0}' extensions are allowed.", string.Join(", ", allowed)], nameof(field.Paths));
        }

        return Task.CompletedTask;
    }
}
