using Grove.ContentManagement;
using Grove.Contents.ViewModels;
using Grove.DisplayManagement.ModelBinding;
using YesSql;

namespace Grove.Contents.Services;

public interface IContentsAdminListQueryService
{
    Task<IQuery<ContentItem>> QueryAsync(ContentOptionsViewModel model, IUpdateModel updater);
}
