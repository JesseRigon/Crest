using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Entities;
using Crest.Indexing.Core;
using Crest.Indexing.Core.Models;
using Crest.Indexing.Models;
using Crest.Indexing.ViewModels;
using Crest.Localization;
using Crest.Mvc.ModelBinding;

namespace Crest.Indexing.Drivers;

internal sealed class ContentIndexProfileDisplayDriver : DisplayDriver<IndexProfile>
{
    private readonly ILocalizationService _localizationService;

    internal readonly IStringLocalizer S;

    public ContentIndexProfileDisplayDriver(ILocalizationService localizationService, IStringLocalizer<ContentIndexProfileDisplayDriver> stringLocalizer)
    {
        _localizationService = localizationService;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(IndexProfile indexProfile, BuildEditorContext context)
    {
        if (!string.Equals(IndexingConstants.ContentsIndexSource, indexProfile.Type, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Initialize<ContentIndexMetadataViewModel>("ContentIndexMetadata_Edit", model =>
        {
            var metadata = indexProfile.GetOrCreate<ContentIndexMetadata>();

            model.IndexLatest = metadata.IndexLatest;
            model.IndexedContentTypes = metadata.IndexedContentTypes;
            model.Culture = metadata.Culture;
            model.Cultures = _localizationService.GetAllCulturesAndAliases()
            .Select(culture => new SelectListItem
            {
                Text = $"{culture.Name} ({culture.DisplayName})",
                Value = culture.Name,
            });

        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(IndexProfile indexProfile, UpdateEditorContext context)
    {
        if (!string.Equals(IndexingConstants.ContentsIndexSource, indexProfile.Type, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var model = new ContentIndexMetadataViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.IndexedContentTypes is null || model.IndexedContentTypes.Length == 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.IndexedContentTypes), S["At least one content type must be selected."]);
        }

        indexProfile.Alter<ContentIndexMetadata>(m =>
        {
            m.IndexLatest = model.IndexLatest;
            m.IndexedContentTypes = model.IndexedContentTypes ?? [];
            m.Culture = model.Culture;
        });

        return Edit(indexProfile, context);
    }
}
