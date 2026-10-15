using System.Text.Json;
using Microsoft.Extensions.Localization;
using Grove.Contents.Management;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Contents.Management.Display.Models;
using Grove.Contents.Management.Metadata.Models;
using Grove.Display.Management.Views;
using Grove.Contents.Taxonomies.Fields;
using Grove.Contents.Taxonomies.Models;
using Grove.Contents.Taxonomies.Settings;
using Grove.Contents.Taxonomies.ViewModels;

namespace Grove.Contents.Taxonomies.Drivers;

public sealed class TaxonomyFieldDisplayDriver : ContentFieldDisplayDriver<TaxonomyField>
{
    private readonly IContentManager _contentManager;

    public TaxonomyFieldDisplayDriver(
        IContentManager contentManager)
    {
        _contentManager = contentManager;
    }

    public override IDisplayResult Display(TaxonomyField field, BuildFieldDisplayContext context)
    {
        return Initialize<DisplayTaxonomyFieldViewModel>(GetDisplayShapeType(context), async model =>
        {
            model.Field = field;
            model.Part = context.ContentPart;
            model.PartFieldDefinition = context.PartFieldDefinition;

            model.Items = await TaxonomyFieldDriverHelper.TermLinksAsync(_contentManager, field);
        }).Location(PlatformConstants.DisplayType.Detail, "Content")
        .Location(PlatformConstants.DisplayType.Summary, "Content");
    }
}
