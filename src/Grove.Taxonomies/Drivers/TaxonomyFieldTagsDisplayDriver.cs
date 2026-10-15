using System.Text.Json;
using System.Text.Json.Nodes;
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

public sealed class TaxonomyFieldTagsDisplayDriver : ContentFieldDisplayDriver<TaxonomyField>
{
    private readonly IContentManager _contentManager;

    public TaxonomyFieldTagsDisplayDriver(
        IContentManager contentManager)
    {
        _contentManager = contentManager;
    }

    public override IDisplayResult Display(TaxonomyField field, BuildFieldDisplayContext context)
    {
        return Initialize<DisplayTaxonomyFieldTagsViewModel>(GetDisplayShapeType(context), async model =>
        {
            model.Items = await TaxonomyFieldDriverHelper.TermLinksAsync(_contentManager, field);
            model.Field = field;
            model.Part = context.ContentPart;
            model.PartFieldDefinition = context.PartFieldDefinition;
        }).Location(PlatformConstants.DisplayType.Detail, "Content")
        .Location(PlatformConstants.DisplayType.Summary, "Content");
    }
}
