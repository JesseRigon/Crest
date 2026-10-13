using System.Security.Claims;
using Crest.Access;
using Crest.Access.Services;
using Crest.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Crest.Tests.Access.Conformance;

/// <summary>
/// Connections that outlive a request (docs/workflows.md › Caller lifetime): the caller is
/// built per operation, so a right lost between two operations fails the next one; the
/// stamp is re-validated once its interval has passed; a revoked identity throws, which
/// tears the connection down.
/// </summary>
public class ConnectionCallerTests
{
    private sealed class Accessor : ICallerContextAccessor
    {
        public CallerContext Current { get; set; }
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Fixture
    {
        public Clock Clock { get; } = new();

        public Accessor Accessor { get; } = new();

        public HashSet<string> Permissions { get; set; } = ["EditContent"];

        public bool StampValid { get; set; } = true;

        public int StampChecks { get; private set; }

        public int Builds { get; private set; }

        public IServiceProvider Services()
        {
            var factory = new Mock<ICallerContextFactory>();
            factory.Setup(f => f.CreateAsync(It.IsAny<CallerRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CallerRequest request, CancellationToken _) =>
                {
                    Builds++;
                    return new CallerContext
                    {
                        Tenant = "Default",
                        Side = request.Side,
                        UserId = "u-editor",
                        OrganizationId = request.RequestedOrganizationId,
                        Permissions = new HashSet<string>(Permissions, StringComparer.OrdinalIgnoreCase),
                    };
                });

            var users = new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null);
            var signIn = new Mock<SignInManager<IUser>>(
                users.Object,
                Mock.Of<IHttpContextAccessor>(),
                Mock.Of<IUserClaimsPrincipalFactory<IUser>>(),
                Options.Create(new IdentityOptions()),
                Mock.Of<global::Microsoft.Extensions.Logging.ILogger<SignInManager<IUser>>>(),
                Mock.Of<IAuthenticationSchemeProvider>(),
                Mock.Of<IUserConfirmation<IUser>>());
            signIn.Setup(s => s.ValidateSecurityStampAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(() =>
                {
                    StampChecks++;
                    return StampValid ? Mock.Of<IUser>() : null;
                });

            return new ServiceCollection()
                .AddSingleton<TimeProvider>(Clock)
                .AddSingleton<ICallerContextAccessor>(Accessor)
                .AddSingleton(factory.Object)
                .AddSingleton(signIn.Object)
                .Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.FromMinutes(30))
                .BuildServiceProvider();
        }
    }

    private static ClaimsPrincipal Cookie() =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "u-editor")], "Identity.Application"));

    private static ConnectionCallerState State(Fixture fixture, CallerSide side = CallerSide.Admin, string organization = null) =>
        new() { Side = side, OrganizationId = organization, StampValidatedAt = fixture.Clock.Now };

    [Fact]
    public async Task TheCallerIsBuiltPerOperationSoALostRightFailsTheNextOne()
    {
        var fixture = new Fixture();
        var services = fixture.Services();
        var state = State(fixture);

        var first = await ConnectionCallers.EnterAsync(services, Cookie(), state);
        Assert.Contains("EditContent", first.Permissions);
        Assert.Same(first, fixture.Accessor.Current);
        ConnectionCallers.Leave(services);
        Assert.Null(fixture.Accessor.Current);

        fixture.Permissions = [];
        var second = await ConnectionCallers.EnterAsync(services, Cookie(), state);
        Assert.DoesNotContain("EditContent", second.Permissions);
        Assert.Equal(2, fixture.Builds);
    }

    [Fact]
    public async Task TheConnectionKeepsItsSideAndOrganization()
    {
        var fixture = new Fixture();
        var caller = await ConnectionCallers.EnterAsync(fixture.Services(), Cookie(), State(fixture, CallerSide.Member, "org-1"));

        Assert.Equal(CallerSide.Member, caller.Side);
        Assert.Equal("org-1", caller.OrganizationId);
    }

    [Fact]
    public async Task TheStampIsRevalidatedOnlyOnceItsIntervalHasPassed()
    {
        var fixture = new Fixture();
        var services = fixture.Services();
        var state = State(fixture);

        await ConnectionCallers.EnterAsync(services, Cookie(), state);
        fixture.Clock.Now += TimeSpan.FromMinutes(10);
        await ConnectionCallers.EnterAsync(services, Cookie(), state);
        Assert.Equal(0, fixture.StampChecks);

        fixture.Clock.Now += TimeSpan.FromMinutes(25);
        await ConnectionCallers.EnterAsync(services, Cookie(), state);
        Assert.Equal(1, fixture.StampChecks);
        Assert.Equal(fixture.Clock.Now, state.StampValidatedAt);
    }

    [Fact]
    public async Task AChangedStampTearsTheConnectionDown()
    {
        var fixture = new Fixture();
        var services = fixture.Services();
        var state = State(fixture);
        fixture.StampValid = false;
        fixture.Clock.Now += TimeSpan.FromMinutes(31);

        await Assert.ThrowsAsync<ConnectionRevokedException>(() => ConnectionCallers.EnterAsync(services, Cookie(), state));
        Assert.Equal(0, fixture.Builds);
        Assert.Null(fixture.Accessor.Current);
    }

    [Fact]
    public async Task ABearerIdentityCarriesNoStampToCheck()
    {
        var fixture = new Fixture { StampValid = false };
        var services = fixture.Services();
        var state = State(fixture);
        fixture.Clock.Now += TimeSpan.FromHours(2);
        var bearer = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "client-1")], "OpenIddict.Validation.AspNetCore"));

        await ConnectionCallers.EnterAsync(services, bearer, state);
        Assert.Equal(0, fixture.StampChecks);
    }
}
