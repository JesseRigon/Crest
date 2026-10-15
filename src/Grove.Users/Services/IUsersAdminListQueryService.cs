using Grove.DisplayManagement.ModelBinding;
using Grove.Users.Models;
using Grove.Users.ViewModels;
using YesSql;

namespace Grove.Users.Services;

public interface IUsersAdminListQueryService
{
    Task<IQuery<User>> QueryAsync(UserIndexOptions options, IUpdateModel updater);
}
