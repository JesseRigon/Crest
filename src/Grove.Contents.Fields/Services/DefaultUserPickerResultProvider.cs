using Microsoft.AspNetCore.Identity;
using Grove.Contents.Management;
using Grove.Security;
using Grove.Access.Users;
using Grove.Access.Users.Indexes;
using Grove.Access.Users.Models;
using YesSql;
using YesSql.Services;

namespace Grove.Contents.Fields.Services;

public class DefaultUserPickerResultProvider : IUserPickerResultProvider
{
    private readonly RoleManager<IRole> _roleManager;
    private readonly UserManager<IUser> _userManager;
    private readonly ISession _session;

    public DefaultUserPickerResultProvider(
        RoleManager<IRole> roleManager,
        UserManager<IUser> userManager,
        ISession session)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _session = session;
    }

    public string Name => "Default";

    public async Task<IEnumerable<UserPickerResult>> Search(UserPickerSearchContext searchContext)
    {
        var query = _session.Query<User>();

        if (!searchContext.DisplayAllUsers)
        {
            var roles = searchContext.Roles.Select(x => _roleManager.NormalizeKey(x));
            query.With<UserByRoleNameIndex>(x => x.RoleName.IsIn(roles));
        }

        if (!string.IsNullOrEmpty(searchContext.Query))
        {
            query.With<UserIndex>(x => x.NormalizedUserName.Contains(_userManager.NormalizeName(searchContext.Query)));
        }

        var users = await query.Take(50).ListAsync();

        var results = new List<UserPickerResult>();

        foreach (var user in users)
        {
            results.Add(new UserPickerResult
            {
                UserId = user.UserId,
                DisplayText = user.UserName,
                IsEnabled = user.IsEnabled,
            });
        }

        return results.OrderBy(x => x.DisplayText);
    }
}
