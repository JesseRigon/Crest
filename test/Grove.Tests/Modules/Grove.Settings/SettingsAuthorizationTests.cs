using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Grove.DisplayManagement;
using Grove.DisplayManagement.Handlers;
using Grove.DisplayManagement.ModelBinding;
using Grove.DisplayManagement.Notify;
using Grove.DisplayManagement.Shapes;
using Grove.DisplayManagement.Zones;
using Grove.Email;
using Grove.Environment.Options;
using Grove.Environment.Shell;
using Grove.Localization;
using Grove.Security;
using Grove.Security.Permissions;
using Grove.Settings;
using Grove.Settings.Drivers;
using Grove.Settings.Services;
using Grove.Settings.ViewModels;
using AdminController = Grove.Settings.Controllers.AdminController;

namespace Grove.Tests.Modules.Grove.Settings;

public class SettingsAuthorizationTests
{
    [Fact]
    public void ManageSettings_GranularBuiltInPermissions_GrantsBoth()
    {
        var claims = new[]
        {
            new Claim(Permission.ClaimType, SettingsPermissions.ManageSettings.Name),
        };

        Assert.True(global::Grove.Tests.Access.ImpliedPermissions.IsGranted(SettingsPermissions.ManageGeneralSettings,
            claims));
        Assert.True(global::Grove.Tests.Access.ImpliedPermissions.IsGranted(SettingsPermissions.ManageDebuggingSettings,
            claims));
    }

    [Fact]
    public async Task PermissionProvider_GranularBuiltInPermissions_ExposesBoth()
    {
        var permissions = await new global::Grove.Settings.Permissions().GetPermissionsAsync();

        Assert.Contains(SettingsPermissions.ManageGeneralSettings, permissions);
        Assert.Contains(SettingsPermissions.ManageDebuggingSettings, permissions);
    }

    [Fact]
    public async Task GroupAuthorization_EmailPermission_GrantsAccess()
    {
        using var serviceProvider = CreateGroupAuthorizationServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<SiteSettingsPermissionOptions>>();
        var mapper = new SiteSettingsPermissionMapper(options);

        // The group's registered permission is a candidate beside the group permission itself.
        var candidates = await mapper.MapAsync(SettingsPermissions.ManageGroupSettings.Name, EmailSettings.GroupId, TestCaller());

        Assert.NotNull(candidates);
        Assert.Contains(candidates, c => c.Permission == EmailPermissions.ManageEmailSettings.Name);
        Assert.Contains(candidates, c => c.Permission == SettingsPermissions.ManageGroupSettings.Name);
    }

    [Fact]
    public async Task GroupAuthorization_UnregisteredGroup_DoesNotGrantAccess()
    {
        using var serviceProvider = CreateGroupAuthorizationServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<SiteSettingsPermissionOptions>>();
        var mapper = new SiteSettingsPermissionMapper(options);

        // An unregistered group maps to nothing: only the group permission itself decides.
        Assert.Null(await mapper.MapAsync(SettingsPermissions.ManageGroupSettings.Name, "legacy", TestCaller()));
    }

    private static global::Grove.Access.CallerContext TestCaller() => new()
    {
        Tenant = "Default",
        Side = global::Grove.Access.CallerSide.Admin,
        UserId = "u1",
    };

    [Fact]
    public async Task DefaultDriver_EditWithGeneralPermission_ReturnsEditor()
    {
        var driver = CreateDefaultDriver(granted: true);
        var context = CreateBuildEditorContext(DefaultSiteSettingsDisplayDriver.GroupId);

        var result = await driver.EditAsync(new SiteSettings(), context);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task DefaultDriver_UpdateWithoutGeneralPermission_DoesNotUpdate()
    {
        var driver = CreateDefaultDriver(granted: false);
        var site = new SiteSettings { SiteName = "Original" };
        var context = CreateUpdateEditorContext(DefaultSiteSettingsDisplayDriver.GroupId);

        var result = await driver.UpdateAsync(site, context);

        Assert.Null(result);
        Assert.Equal("Original", site.SiteName);
    }

    [Fact]
    public async Task DebugDriver_EditWithDebuggingPermission_ReturnsEditor()
    {
        var driver = CreateDebugDriver(granted: true);
        var context = CreateBuildEditorContext(DebugSettingsDisplayDriver.GroupId);

        var result = await driver.EditAsync(new SiteSettings(), new DebugSettings(), context);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task DebugDriver_UpdateWithoutDebuggingPermission_DoesNotUpdate()
    {
        var driver = CreateDebugDriver(granted: false);
        var settings = new DebugSettings { WriteShapeDebugInformation = true };
        var context = CreateUpdateEditorContext(DebugSettingsDisplayDriver.GroupId);

        var result = await driver.UpdateAsync(new SiteSettings(), settings, context);

        Assert.Null(result);
        Assert.True(settings.WriteShapeDebugInformation);
    }

    [Fact]
    public async Task Index_AuthorizedSpecializedEditor_ReturnsView()
    {
        var shape = await CreateEditorShapeAsync(hasEditorContent: true);
        var displayManager = new Mock<IDisplayManager<ISite>>();
        displayManager
            .Setup(x => x.BuildEditorAsync(
                It.IsAny<ISite>(),
                It.IsAny<IUpdateModel>(),
                false,
                "email",
                string.Empty))
            .ReturnsAsync(shape);
        var siteService = CreateSiteService();
        var controller = CreateController(displayManager.Object, siteService.Object, granted: true);

        var result = await controller.Index("email");

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminIndexViewModel>(viewResult.Model);
        Assert.Same(shape, model.Shape);
    }

    [Fact]
    public async Task IndexPost_NoAuthorizedEditorContent_DoesNotSave()
    {
        var shape = await CreateEditorShapeAsync(hasEditorContent: false);
        var displayManager = new Mock<IDisplayManager<ISite>>();
        displayManager
            .Setup(x => x.UpdateEditorAsync(
                It.IsAny<ISite>(),
                It.IsAny<IUpdateModel>(),
                false,
                "email",
                string.Empty))
            .ReturnsAsync(shape);
        var siteService = CreateSiteService();
        var controller = CreateController(displayManager.Object, siteService.Object, granted: true);

        var result = await controller.IndexPost("email");

        Assert.IsType<NotFoundResult>(result);
        siteService.Verify(x => x.UpdateSiteSettingsAsync(It.IsAny<ISite>()), Times.Never);
    }

    private static DefaultSiteSettingsDisplayDriver CreateDefaultDriver(bool granted)
        => new(
            CreateHttpContextAccessor(),
            CreateAuthorizationService(SettingsPermissions.ManageGeneralSettings, granted),
            Mock.Of<IShellReleaseManager>(),
            Mock.Of<IStringLocalizer<DefaultSiteSettingsDisplayDriver>>());

    private static DebugSettingsDisplayDriver CreateDebugDriver(bool granted)
        => new(
            CreateHttpContextAccessor(),
            CreateAuthorizationService(SettingsPermissions.ManageDebuggingSettings, granted),
            Mock.Of<IOptionsUpdateNotifier>());

    private static HttpContextAccessor CreateHttpContextAccessor()
        => new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext(),
        };

    private static IAuthorizationService CreateAuthorizationService(Permission permission, bool granted)
    {
        var authorizationService = new Mock<IAuthorizationService>();
        authorizationService
            .Setup(x => x.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<object>(),
                It.Is<IEnumerable<IAuthorizationRequirement>>(requirements =>
                    requirements.OfType<PermissionRequirement>().Any(requirement =>
                        requirement.Permission.Name == permission.Name))))
            .ReturnsAsync(granted ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        return authorizationService.Object;
    }

    private static IAuthorizationService CreatePermissionAuthorizationService()
    {
        var authorizationService = new Mock<IAuthorizationService>();
        authorizationService
            .Setup(x => x.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<object>(),
                It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .Returns<ClaimsPrincipal, object, IEnumerable<IAuthorizationRequirement>>((user, _, requirements) =>
            {
                var isGranted = requirements
                    .OfType<PermissionRequirement>()
                    .All(requirement => global::Grove.Tests.Access.ImpliedPermissions.IsGranted(requirement.Permission, user.Claims));

                return Task.FromResult(isGranted ? AuthorizationResult.Success() : AuthorizationResult.Failed());
            });

        return authorizationService.Object;
    }

    private static ServiceProvider CreateGroupAuthorizationServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddSiteSettingsPermission(EmailSettings.GroupId, EmailPermissions.ManageEmailSettings);
        services.AddSingleton(CreatePermissionAuthorizationService());

        return services.BuildServiceProvider();
    }

    private static BuildEditorContext CreateBuildEditorContext(string groupId)
        => new(new Shape(), groupId, false, string.Empty, null, null, null);

    private static UpdateEditorContext CreateUpdateEditorContext(string groupId)
        => new(new Shape(), groupId, false, string.Empty, null, null, null);

    private static Mock<ISiteService> CreateSiteService()
    {
        var site = new SiteSettings();
        var siteService = new Mock<ISiteService>();
        siteService.Setup(x => x.GetSiteSettingsAsync()).ReturnsAsync(site);
        siteService.Setup(x => x.LoadSiteSettingsAsync()).ReturnsAsync(site);

        return siteService;
    }

    private static AdminController CreateController(
        IDisplayManager<ISite> displayManager,
        ISiteService siteService,
        bool granted)
        => new(
                Mock.Of<IShellReleaseManager>(),
                siteService,
                displayManager,
                CreateAuthorizationService(SettingsPermissions.ManageGroupSettings, granted),
                Mock.Of<INotifier>(),
                Options.Create(new CultureOptions()),
                Mock.Of<IUpdateModelAccessor>(),
                Mock.Of<IHtmlLocalizer<AdminController>>())
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "Test")),
                    },
                },
            };

    private static async Task<IShape> CreateEditorShapeAsync(bool hasEditorContent)
    {
        var shape = new ZoneHolding(() => ValueTask.FromResult<IShape>(new Shape()));
        var actions = new Shape();
        await actions.AddAsync(new Shape());
        shape.Properties["Actions"] = actions;

        if (hasEditorContent)
        {
            var content = new Shape();
            await content.AddAsync(new Shape());
            shape.Properties["Content"] = content;
        }

        return shape;
    }
}
