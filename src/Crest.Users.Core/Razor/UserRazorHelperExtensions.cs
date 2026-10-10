using Microsoft.Extensions.DependencyInjection;
using Crest;
using Crest.Users.Indexes;
using Crest.Users.Models;
using YesSql;
using YesSql.Services;

#pragma warning disable CA1050 // Declare types in namespaces
public static class UserRazorHelperExtensions
#pragma warning restore CA1050 // Declare types in namespaces
{
    /// <summary>
    /// Returns a <see cref="User"/> by its <see cref="User.UserId"/>.
    /// </summary>
    /// <param name="platformHelper">The <see cref="IPlatformHelper"/>.</param>
    /// <param name="userId">The <see cref="User.UserId"/>.</param>
    /// <returns>A <see cref="User"/> or <c>null</c> if it was not found.</returns>
    public static Task<User> GetUserByIdAsync(this IPlatformHelper platformHelper, string userId)
    {
        var session = platformHelper.HttpContext.RequestServices.GetService<ISession>();
        return session.Query<User, UserIndex>(x => x.UserId == userId).FirstOrDefaultAsync();
    }

    /// <summary>
    /// Loads a list of users by their user ids./>.
    /// </summary>
    /// <param name="platformHelper">The <see cref="IPlatformHelper"/>.</param>
    /// <param name="userIds">The user ids to load.</param>
    /// <returns>A list of users with the specific ids.</returns>
    public static async Task<IEnumerable<User>> GetUsersByIdsAsync(this IPlatformHelper platformHelper, IEnumerable<string> userIds)
    {
        var session = platformHelper.HttpContext.RequestServices.GetService<ISession>();
        return await session.Query<User, UserIndex>(x => x.UserId.IsIn(userIds)).ListAsync();
    }
}
