using Microsoft.Extensions.Localization;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Metadata.Models;
using Grove.Data.Types.Spatial.Fields;
using Grove.Data.Types.Spatial.Settings;

namespace Grove.Data.Types.Spatial.Handlers;

public class GeoPointFieldHandler : ContentFieldHandler<GeoPointField>
{
    protected readonly IStringLocalizer S;

    public GeoPointFieldHandler(IStringLocalizer<GeoPointFieldHandler> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task ValidatingAsync(ValidateContentFieldContext context, GeoPointField field)
    {
        var settings = context.ContentFieldDefinition.GetSettings<GeoPointFieldSettings>();

        if (settings.Required)
        {
            if (!field.Latitude.HasValue)
            {
                context.Fail(S["The {0} field is required.", context.ContentFieldDefinition.DisplayName()], nameof(field.Latitude));
            }

            if (!field.Longitude.HasValue)
            {
                context.Fail(S["The {0} field is required.", context.ContentFieldDefinition.DisplayName()], nameof(field.Longitude));
            }
        }

        if (field.Latitude is < -90 or > 90)
        {
            context.Fail(S["The latitude of {0} must be between -90 and 90.", context.ContentFieldDefinition.DisplayName()], nameof(field.Latitude));
        }

        if (field.Longitude is < -180 or > 180)
        {
            context.Fail(S["The longitude of {0} must be between -180 and 180.", context.ContentFieldDefinition.DisplayName()], nameof(field.Longitude));
        }

        return Task.CompletedTask;
    }
}
