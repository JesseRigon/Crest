using Grove.ContentLocalization.Models;
using Grove.ContentLocalization.ViewModels;
using Grove.ContentManagement;
using Grove.ContentManagement.Metadata;
using Grove.ContentManagement.Records;
using Grove.Contents.Services;
using Grove.Contents.ViewModels;
using Grove.DisplayManagement.ModelBinding;
using YesSql;
using YesSql.Services;

namespace Grove.ContentLocalization.Services;

public class LocalizationPartContentsAdminListFilter : IContentsAdminListFilter
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public LocalizationPartContentsAdminListFilter(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task FilterAsync(ContentOptionsViewModel model, IQuery<ContentItem> query, IUpdateModel updater)
    {
        var viewModel = new LocalizationContentsAdminFilterViewModel();
        await updater.TryUpdateModelAsync(viewModel, "Localization");

        // Show localization content items
        // This is intended to be used by adding ?Localization.ShowLocalizedContentTypes to an AdminMenu url.
        if (viewModel.ShowLocalizedContentTypes)
        {
            var localizedTypes = (await _contentDefinitionManager.ListTypeDefinitionsAsync())
                .Where(x =>
                    x.Parts.Any(p =>
                        p.PartDefinition.Name == nameof(LocalizationPart)))
                .Select(x => x.Name);

            query.With<ContentItemIndex>(x => x.ContentType.IsIn(localizedTypes));

        }
    }
}
