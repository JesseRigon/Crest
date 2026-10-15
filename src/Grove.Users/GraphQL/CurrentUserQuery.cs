using GraphQL.Resolvers;
using GraphQL.Types;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Grove.Apis.GraphQL;
using Grove.ContentManagement.Metadata;
using Grove.Users.Models;
using Grove.Users.Services;

namespace Grove.Users.GraphQL;

/// <summary>
/// Registers the current user including its custom user settings as a query.
/// </summary>
internal sealed class CurrentUserQuery : ISchemaBuilder
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    public CurrentUserQuery(
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<CurrentUserQuery> localizer)
    {
        _httpContextAccessor = httpContextAccessor;
        S = localizer;
    }

    public Task BuildAsync(ISchema schema)
    {
        var currentUserField = new FieldType
        {
            Name = "me",
            Description = S["Gets the currently authenticated user."],
            Type = typeof(UserType),
            Resolver = new FuncFieldResolver<User>(async context =>
            {
                var userService = context.RequestServices.GetRequiredService<IUserService>();
                var user = await userService.GetAuthenticatedUserAsync(((GraphQLUserContext)context.UserContext).User);

                return user as User;
            }),
        };

        schema.Query.AddField(currentUserField);

        return Task.CompletedTask;
    }

    public Task<string> GetIdentifierAsync()
    {
        var contentDefinitionManager = _httpContextAccessor.HttpContext.RequestServices.GetRequiredService<IContentDefinitionManager>();

        return contentDefinitionManager.GetIdentifierAsync();
    }
}
