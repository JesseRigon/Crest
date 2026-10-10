using Crest.ContentManagement;
using Crest.Contents.ViewModels;
using Crest.DisplayManagement.ModelBinding;
using YesSql;

namespace Crest.Contents.Services;

public interface IContentsAdminListQueryService
{
    Task<IQuery<ContentItem>> QueryAsync(ContentOptionsViewModel model, IUpdateModel updater);
}
