using Grove.DisplayManagement.ModelBinding;
using Grove.Users.Models;
using Grove.Users.ViewModels;
using YesSql;

namespace Grove.Users;

public interface IUsersAdminListFilter
{
    Task FilterAsync(UserIndexOptions model, IQuery<User> query, IUpdateModel updater);
}
