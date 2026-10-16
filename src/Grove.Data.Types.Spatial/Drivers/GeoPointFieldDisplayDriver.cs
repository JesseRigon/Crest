using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Localization;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Contents.Management.Display.Models;
using Grove.Contents.Management.Metadata.Models;
using Grove.Display.Management.Views;
using Grove.Data.Types.Spatial.Fields;
using Grove.Data.Types.Spatial.Settings;
using Grove.Data.Types.Spatial.ViewModels;

namespace Grove.Data.Types.Spatial.Drivers;

public sealed class GeoPointFieldDisplayDriver : ContentFieldDisplayDriver<GeoPointField>
{
    public override IDisplayResult Display(GeoPointField field, BuildFieldDisplayContext context)
    {
        return Initialize<DisplayGeoPointFieldViewModel>(GetDisplayShapeType(context), model =>
        {
            model.Field = field;
            model.FieldSet = context.ContentFieldSet;
            model.FieldDefinition = context.FieldDefinition;
        }).Location(PlatformConstants.DisplayType.Detail, "Content")
        .Location(PlatformConstants.DisplayType.Summary, "Content");
    }
}
