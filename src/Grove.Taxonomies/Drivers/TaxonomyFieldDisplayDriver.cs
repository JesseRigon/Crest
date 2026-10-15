using System.Text.Json;
using Microsoft.Extensions.Localization;
using Grove.ContentManagement;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentManagement.Display.Models;
using Grove.ContentManagement.Metadata.Models;
using Grove.DisplayManagement.Views;
using Grove.Taxonomies.Fields;
using Grove.Taxonomies.Models;
using Grove.Taxonomies.Settings;
using Grove.Taxonomies.ViewModels;

namespace Grove.Taxonomies.Drivers;

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
