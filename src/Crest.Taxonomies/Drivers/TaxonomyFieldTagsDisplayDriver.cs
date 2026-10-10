using System.Text.Json;
using System.Text.Json.Nodes;
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

public sealed class TaxonomyFieldTagsDisplayDriver : ContentFieldDisplayDriver<TaxonomyField>
{
    private readonly IContentManager _contentManager;

    internal readonly IStringLocalizer S;

    public TaxonomyFieldTagsDisplayDriver(
        IContentManager contentManager,
        IStringLocalizer<TaxonomyFieldTagsDisplayDriver> stringLocalizer)
    {
        _contentManager = contentManager;
        S = stringLocalizer;
    }

    public override IDisplayResult Display(TaxonomyField field, BuildFieldDisplayContext context)
    {
        return Initialize<DisplayTaxonomyFieldTagsViewModel>(GetDisplayShapeType(context), model =>
        {
            model.Field = field;
            model.Part = context.ContentPart;
            model.PartFieldDefinition = context.PartFieldDefinition;
        }).Location(PlatformConstants.DisplayType.Detail, "Content")
        .Location(PlatformConstants.DisplayType.Summary, "Content");
    }

    public override IDisplayResult Edit(TaxonomyField field, BuildFieldEditorContext context)
    {
        return Initialize<EditTagTaxonomyFieldViewModel>(GetEditorShapeType(context), async model =>
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
                var tagTermEntries = termEntries.Select(te => new TagTermEntry
                {
                    ContentItemId = te.ContentItemId,
                    Selected = te.Selected,
                    DisplayText = te.Term.DisplayText,
                    IsLeaf = te.IsLeaf,
                });

                model.TagTermEntries = JNode.FromObject(tagTermEntries, JOptions.CamelCase).ToJsonString(JOptions.Default);
            }

            model.Field = field;
            model.Part = context.ContentPart;
            model.PartFieldDefinition = context.PartFieldDefinition;
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(TaxonomyField field, UpdateFieldEditorContext context)
    {
        var model = new EditTagTaxonomyFieldViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix, f => f.TermContentItemIds);

        var settings = context.PartFieldDefinition.GetSettings<TaxonomyFieldSettings>();

        field.TaxonomyContentItemId = settings.TaxonomyContentItemId;

        field.TermContentItemIds = model.TermContentItemIds == null
            ? [] : model.TermContentItemIds.Split(',', StringSplitOptions.RemoveEmptyEntries);

        if (settings.Required && field.TermContentItemIds.Length == 0)
        {
            context.Updater.ModelState.AddModelError(
                $"{Prefix}.{nameof(EditTagTaxonomyFieldViewModel.TermContentItemIds)}",
                S["A value is required for {0}.", context.PartFieldDefinition.DisplayName()]);
        }

        // Update display text for tags.
        var taxonomy = await _contentManager.GetAsync(settings.TaxonomyContentItemId, VersionOptions.Latest);

        if (taxonomy == null)
        {
            return null;
        }

        var terms = new List<ContentItem>();

        foreach (var termContentItemId in field.TermContentItemIds)
        {
            var term = TaxonomyPlatformHelperExtensions.FindTerm(
                (JsonArray)taxonomy.Content["TaxonomyPart"]["Terms"],
                termContentItemId);

            terms.Add(term);
        }

        field.SetTagNames(terms.Select(t => t.DisplayText).ToArray());

        return Edit(field, context);
    }
}
