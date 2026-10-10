using Microsoft.Extensions.Localization;
using Crest.ContentManagement;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Display.Models;
using Crest.ContentManagement.Metadata.Models;
using Crest.DisplayManagement.Views;
using Crest.Taxonomies.Fields;
using Crest.Taxonomies.Models;
using Crest.Taxonomies.Settings;
using Crest.Taxonomies.ViewModels;

namespace Crest.Taxonomies.Drivers;

public sealed class TaxonomyFieldDisplayDriver : ContentFieldDisplayDriver<TaxonomyField>
{
    private readonly IContentManager _contentManager;

    internal readonly IStringLocalizer S;

    public TaxonomyFieldDisplayDriver(
        IContentManager contentManager,
        IStringLocalizer<TaxonomyFieldDisplayDriver> localizer)
    {
        _contentManager = contentManager;
        S = localizer;
    }

    public override IDisplayResult Display(TaxonomyField field, BuildFieldDisplayContext context)
    {
        return Initialize<DisplayTaxonomyFieldViewModel>(GetDisplayShapeType(context), model =>
        {
            model.Field = field;
            model.Part = context.ContentPart;
            model.PartFieldDefinition = context.PartFieldDefinition;
        }).Location(PlatformConstants.DisplayType.Detail, "Content")
        .Location(PlatformConstants.DisplayType.Summary, "Content");
    }

    public override IDisplayResult Edit(TaxonomyField field, BuildFieldEditorContext context)
    {
        return Initialize<EditTaxonomyFieldViewModel>(GetEditorShapeType(context), async model =>
        {
            var settings = context.PartFieldDefinition.GetSettings<TaxonomyFieldSettings>();
            model.Taxonomy = await _contentManager.GetAsync(settings.TaxonomyContentItemId, VersionOptions.Latest);

            if (model.Taxonomy != null)
            {
                var termEntries = new List<TermEntry>();

                if (!model.Taxonomy.TryGet<TaxonomyPart>(out var taxonomyPart))
                {
                    model.Field = field;
                    model.Part = context.ContentPart;
                    model.PartFieldDefinition = context.PartFieldDefinition;
                    return;
                }

                var terms = taxonomyPart.Terms;

                // Maintain the listed order in the field, then concatenate the remaining content items.
                var sortedTerms = terms
                    .Where(x => field.TermContentItemIds.Contains(x.ContentItemId))
                    .OrderBy(x => Array.IndexOf(field.TermContentItemIds, x.ContentItemId))
                    .Concat(terms.Where(x => !field.TermContentItemIds.Contains(x.ContentItemId)))
                    .ToArray();

                TaxonomyFieldDriverHelper.PopulateTermEntries(termEntries, field, sortedTerms, 0);

                model.TermEntries = termEntries;
                model.UniqueValue = termEntries.FirstOrDefault(x => x.Selected)?.ContentItemId;
            }

            model.Field = field;
            model.Part = context.ContentPart;
            model.PartFieldDefinition = context.PartFieldDefinition;
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(TaxonomyField field, UpdateFieldEditorContext context)
    {
        var model = new EditTaxonomyFieldViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var settings = context.PartFieldDefinition.GetSettings<TaxonomyFieldSettings>();

        field.TaxonomyContentItemId = settings.TaxonomyContentItemId;
        field.TermContentItemIds = model.TermEntries.Where(x => x.Selected).Select(x => x.ContentItemId).ToArray();

        if (settings.Unique && !string.IsNullOrEmpty(model.UniqueValue))
        {
            field.TermContentItemIds = [model.UniqueValue];
        }

        if (settings.Required && field.TermContentItemIds.Length == 0)
        {
            context.Updater.ModelState.AddModelError(
                $"{Prefix}.{nameof(EditTaxonomyFieldViewModel.TermEntries)}",
                S["A value is required for {0}.", context.PartFieldDefinition.DisplayName()]);
        }

        return Edit(field, context);
    }
}
