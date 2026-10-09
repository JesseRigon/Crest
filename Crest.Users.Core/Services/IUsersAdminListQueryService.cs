using Crest.DisplayManagement.ModelBinding;
using Crest.Users.Models;
using Crest.Users.ViewModels;
using YesSql;

namespace Crest.Users.Services;

public interface IUsersAdminListQueryService
{
    Task<IQuery<User>> QueryAsync(UserIndexOptions options, IUpdateModel updater);
}
