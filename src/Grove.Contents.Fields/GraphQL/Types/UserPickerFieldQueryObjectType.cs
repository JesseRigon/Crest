using GraphQL;
using GraphQL.DataLoader;
using GraphQL.Types;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Grove.Data.Protocols.GraphQL;
using Grove.Contents.Fields.Fields;
using Grove.Access.Users;
using Grove.Access.Users.GraphQL;
using Grove.Access.Users.Indexes;
using Grove.Access.Users.Models;
using YesSql;
using YesSql.Services;

namespace Grove.Contents.Fields.GraphQL;

public class UserPickerFieldQueryObjectType : ObjectGraphType<UserPickerField>
{
    public UserPickerFieldQueryObjectType(IStringLocalizer<UserPickerFieldQueryObjectType> S)
    {
        Name = nameof(UserPickerField);

        Field<ListGraphType<StringGraphType>, IEnumerable<string>>("userIds")
            .Description(S["user ids"])
            .PagingArguments()
            .Resolve(resolve => resolve.Page(resolve.Source.UserIds));

        Field<ListGraphType<UserType>, IEnumerable<User>>("users")
            .Description(S["the user items"])
            .PagingArguments()
            .RequirePermission(UsersPermissions.ViewUsers)
            .ResolveAsync(resolve =>
            {
                var userLoader = GetOrAddUserProfileByIdDataLoader(resolve);
                return userLoader.LoadAsync(resolve.Page(resolve.Source.UserIds)).Then(itemResultSet =>
                {
                    return itemResultSet.SelectMany(users => users);
                });
            });
    }

    private static IDataLoader<string, IEnumerable<User>> GetOrAddUserProfileByIdDataLoader<T>(IResolveFieldContext<T> context)
    {
        var dataLoaderContextAccessor = context.RequestServices.GetRequiredService<IDataLoaderContextAccessor>();

        return dataLoaderContextAccessor.Context.GetOrAddCollectionBatchLoader("GetOrAddUserByIds", async (IEnumerable<string> userIds) =>
        {
            if (userIds == null || !userIds.Any())
            {
                return default;
            }

            var session = context.RequestServices.GetService<ISession>();
            var users = await session.Query<User, UserIndex>(user => user.UserId.IsIn(userIds)).ListAsync();

            return users.ToLookup(user => user.UserId);
        });
    }
}
