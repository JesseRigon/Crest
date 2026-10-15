using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Localization;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentManagement.Display.Models;
using Grove.ContentManagement.Metadata.Models;
using Grove.DisplayManagement.Views;
using Grove.Mvc.ModelBinding;
using Grove.Spatial.Fields;
using Grove.Spatial.Settings;
using Grove.Spatial.ViewModels;

namespace Grove.Spatial.Drivers;

public sealed class GeoPointFieldDisplayDriver : ContentFieldDisplayDriver<GeoPointField>
{
    public override IDisplayResult Display(GeoPointField field, BuildFieldDisplayContext context)
    {
        return Initialize<DisplayGeoPointFieldViewModel>(GetDisplayShapeType(context), model =>
        {
            model.Field = field;
            model.Part = context.ContentPart;
            model.PartFieldDefinition = context.PartFieldDefinition;
        }).Location(PlatformConstants.DisplayType.Detail, "Content")
        .Location(PlatformConstants.DisplayType.Summary, "Content");
    }
}
