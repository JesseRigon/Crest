using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;
using Grove.ContentFields.Fields;
using Grove.ContentFields.Settings;
using Grove.ContentManagement.Handlers;
using Grove.ContentManagement.Metadata.Models;

namespace Grove.ContentFields.Handlers;

public class YoutubeFieldHandler : ContentFieldHandler<YoutubeField>
{
    protected readonly IStringLocalizer S;

    public YoutubeFieldHandler(IStringLocalizer<YoutubeFieldHandler> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <summary>
    /// Derives the embed address from the raw address the editor stores (the native editor
    /// writes only the raw address): "watch?v=ID" and "youtu.be/ID" become "/embed/ID".
    /// </summary>
    public override Task UpdatingAsync(UpdateContentFieldContext context, YoutubeField field)
    {
        var embedded = EmbedAddress(field.RawAddress);
        if (!string.Equals(embedded, field.EmbeddedAddress, StringComparison.Ordinal))
        {
            field.EmbeddedAddress = embedded;
            field.Content.EmbeddedAddress = embedded;
        }

        return Task.CompletedTask;
    }

    /// <summary>The embed address of a YouTube address; null when there is none or it has no video id.</summary>
    public static string EmbedAddress(string rawAddress)
    {
        if (string.IsNullOrWhiteSpace(rawAddress) || !Uri.TryCreate(rawAddress, UriKind.Absolute, out var uri))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(uri.Query))
        {
            return QueryHelpers.ParseQuery(uri.Query).TryGetValue("v", out var values)
                ? $"{uri.GetLeftPart(UriPartial.Authority)}/embed/{values}"
                : null;
        }

        return $"{uri.GetLeftPart(UriPartial.Authority)}/embed/{uri.AbsolutePath.Split('?')[0].TrimStart('/')}";
    }

    public override Task ValidatingAsync(ValidateContentFieldContext context, YoutubeField field)
    {
        var settings = context.ContentPartFieldDefinition.GetSettings<YoutubeFieldSettings>();

        if (settings.Required && string.IsNullOrWhiteSpace(field.RawAddress))
        {
            context.Fail(S["A value is required for '{0}'.", context.ContentPartFieldDefinition.DisplayName()], nameof(field.RawAddress));
        }

        if (field.RawAddress != null)
        {
            var uri = new Uri(field.RawAddress);

            // if it is a url with QueryString
            if (!string.IsNullOrWhiteSpace(uri.Query))
            {
                var query = QueryHelpers.ParseQuery(uri.Query);
                if (!query.ContainsKey("v"))
                {
                    context.Fail(S["The format of the url is invalid"], nameof(field.RawAddress));
                }
            }
        }

        return Task.CompletedTask;
    }
}
