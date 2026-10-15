using Grove.ContentManagement;
using Grove.ContentManagement.Metadata;
using Grove.ContentManagement.Records;
using Grove.Contents.Services;
using Grove.Contents.ViewModels;
using Grove.DisplayManagement.ModelBinding;
using Grove.Lists.Indexes;
using Grove.Lists.Models;
using Grove.Lists.ViewModels;
using YesSql;
using YesSql.Services;

namespace Grove.Lists.Services;

public class ListPartContentsAdminListFilter : IContentsAdminListFilter
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public ListPartContentsAdminListFilter(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task FilterAsync(ContentOptionsViewModel model, IQuery<ContentItem> query, IUpdateModel updater)
    {
        var viewModel = new ListPartContentsAdminFilterViewModel();
        await updater.TryUpdateModelAsync(viewModel, nameof(ListPart));

        // Show list content items
        if (viewModel.ShowListContentTypes)
        {
            var listableTypes = (await _contentDefinitionManager.ListTypeDefinitionsAsync())
                .Where(x =>
                    x.Parts.Any(p =>
                        p.PartDefinition.Name == nameof(ListPart)))
                .Select(x => x.Name);

            query.With<ContentItemIndex>(x => x.ContentType.IsIn(listableTypes));
        }

        // Show contained elements for the specified list
        else if (viewModel.ListContentItemId != null)
        {
            query.With<ContainedPartIndex>(x => x.ListContentItemId == viewModel.ListContentItemId);
        }
    }
}
