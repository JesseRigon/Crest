using Grove.Users.Models;
using YesSql.Filters.Query;

namespace Grove.Users.Services;

public interface IUsersAdminListFilterParser : IQueryParser<User>;
