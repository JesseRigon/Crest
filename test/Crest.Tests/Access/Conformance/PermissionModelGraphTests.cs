using Crest.Access;
using Crest.Access.Services;
using Crest.Entities;
using Crest.Security;
using Crest.Settings;
using Crest.Users;
using Crest.Users.Handlers;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using YesSql;

namespace Crest.Tests.Access.Conformance;

/// <summary>
/// The permission model's services resolve beside the user manager (regression, 2026-10-13: a
/// fresh tenant never finished setup). The user store takes every user event handler; Members'
/// handlers reach the group service and the caller factory, both of which reach the
/// projector; a projector holding the user manager in its constructor closed the loop
/// (UserAccessProjector → UserManager → UserStore → IUserEventHandler → … → IUserAccessReader
/// → UserAccessProjector) and every scope's resolution recursed without end.
/// </summary>
public class PermissionModelGraphTests
{
    private sealed class Store(IEnumerable<IUserEventHandler> handlers) : IUserStore<IUser>
    {
        public IEnumerable<IUserEventHandler> Handlers { get; } = handlers;

        public void Dispose() { }
        public Task<IdentityResult> CreateAsync(IUser user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IdentityResult> DeleteAsync(IUser user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IUser> FindByIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<IUser>(null);
        public Task<IUser> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) => Task.FromResult<IUser>(null);
        public Task<string> GetNormalizedUserNameAsync(IUser user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<string> GetUserIdAsync(IUser user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<string> GetUserNameAsync(IUser user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SetNormalizedUserNameAsync(IUser user, string normalizedName, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SetUserNameAsync(IUser user, string userName, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IdentityResult> UpdateAsync(IUser user, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    /// <summary>The shape of Members' binding stamp: a user event handler that places users in groups.</summary>
    private sealed class PlacingHandler(IAccessGroupService groups) : UserEventHandlerBase
    {
        public IAccessGroupService Groups { get; } = groups;
    }

    /// <summary>A user event handler that reads the projection (the caller factory's shape).</summary>
    private sealed class ReadingHandler(IUserAccessReader reader) : UserEventHandlerBase
    {
        public IUserAccessReader Reader { get; } = reader;
    }

    [Fact]
    public async Task The_user_manager_and_the_projection_resolve_in_one_scope()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => Mock.Of<YesSql.ISession>());
        services.AddScoped(_ => Mock.Of<global::Crest.Entities.IIdGenerator>());
        services.AddScoped(_ => Mock.Of<IPermissionVersion>());
        services.AddScoped(_ => Mock.Of<ISiteService>());
        services.AddScoped(_ => Mock.Of<IPermissionSetService>());
        services.AddScoped(_ => new Mock<RoleManager<IRole>>(Mock.Of<IRoleStore<IRole>>(), Array.Empty<IRoleValidator<IRole>>(), null, null, null).Object);
        services.AddScoped<IUserStore<IUser>, Store>();
        services.AddScoped(sp => new UserManager<IUser>(sp.GetRequiredService<IUserStore<IUser>>(), null, null, null, null, null, null, sp, null));
        services.AddScoped<IUserEventHandler, PlacingHandler>();
        services.AddScoped<IUserEventHandler, ReadingHandler>();
        global::Crest.Access.Startup.AddPermissionModelServices(services);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        // A cycle recurses on fresh stacks without end rather than throwing: bound the wait.
        var resolve = Task.Run(() =>
        {
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<UserManager<IUser>>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<IUserAccessReader>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<IAccessGroupService>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<IUserAccessOverrideService>());
        }, TestContext.Current.CancellationToken);

        await resolve.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
    }

    [Fact]
    public void The_projector_takes_no_user_manager_or_store_in_its_constructor()
    {
        var parameters = typeof(UserAccessProjector).GetConstructors().Single().GetParameters().Select(parameter => parameter.ParameterType).ToArray();

        Assert.DoesNotContain(typeof(UserManager<IUser>), parameters);
        Assert.DoesNotContain(typeof(IUserStore<IUser>), parameters);
    }
}
