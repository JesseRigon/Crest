using Crest.ContentManagement;
using Crest.ContentManagement.Metadata;
using Crest.ContentManagement.Records;
using Crest.Contents.Services;
using Crest.Contents.ViewModels;
using Crest.DisplayManagement.ModelBinding;
using Crest.Lists.Indexes;
using Crest.Lists.Models;
using Crest.Lists.ViewModels;
using YesSql;
using YesSql.Services;

namespace Crest.Lists.Services;

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
