using Crest.DisplayManagement.ModelBinding;
using Crest.Users.Models;
using Crest.Users.ViewModels;
using YesSql;

namespace Crest.Users;

public interface IUsersAdminListFilter
{
    Task FilterAsync(UserIndexOptions model, IQuery<User> query, IUpdateModel updater);
}
