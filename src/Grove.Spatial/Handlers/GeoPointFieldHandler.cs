using Microsoft.Extensions.Localization;
using Grove.ContentManagement.Handlers;
using Grove.ContentManagement.Metadata.Models;
using Grove.Spatial.Fields;
using Grove.Spatial.Settings;

namespace Grove.Spatial.Handlers;

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

        if (field.Latitude is < -90 or > 90)
        {
            context.Fail(S["The latitude of {0} must be between -90 and 90.", context.ContentPartFieldDefinition.DisplayName()], nameof(field.Latitude));
        }

        if (field.Longitude is < -180 or > 180)
        {
            context.Fail(S["The longitude of {0} must be between -180 and 180.", context.ContentPartFieldDefinition.DisplayName()], nameof(field.Longitude));
        }

        return Task.CompletedTask;
    }
}
