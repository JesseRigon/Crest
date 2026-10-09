using Microsoft.Extensions.Localization;
using Crest.ContentManagement.Handlers;
using Crest.ContentManagement.Metadata.Models;
using Crest.Spatial.Fields;
using Crest.Spatial.Settings;

namespace Crest.Spatial.Handlers;

public class GeoPointFieldHandler : ContentFieldHandler<GeoPointField>
{
    protected readonly IStringLocalizer S;

    public GeoPointFieldHandler(IStringLocalizer<GeoPointFieldHandler> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task ValidatingAsync(ValidateContentFieldContext context, GeoPointField field)
    {
        var settings = context.ContentPartFieldDefinition.GetSettings<GeoPointFieldSettings>();

        if (settings.Required)
        {
            if (!field.Latitude.HasValue)
            {
                context.Fail(S["The {0} field is required.", context.ContentPartFieldDefinition.DisplayName()], nameof(field.Latitude));
            }

            if (!field.Longitude.HasValue)
            {
                context.Fail(S["The {0} field is required.", context.ContentPartFieldDefinition.DisplayName()], nameof(field.Longitude));
            }
        }

        return Task.CompletedTask;
    }
}
