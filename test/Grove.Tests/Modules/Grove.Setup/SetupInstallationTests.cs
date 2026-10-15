using Grove.Abstractions.Setup;
using Grove.Data;
using Grove.Email;
using Grove.Environment.Shell;
using Grove.Recipes.Models;
using Grove.Setup.Models;
using Grove.Setup.Services;

namespace Grove.Modules.Grove.Setup.Tests;

public class SetupInstallationTests
{
    [Fact]
    public async Task Choices_TablePrefixIsPreset_KeepsDatabaseOptionsVisible()
    {
        var installation = CreateInstallation(new ShellSettings
        {
            ["TablePrefix"] = "Default",
        });

        var choices = await installation.GetChoicesAsync();
        var form = await installation.NewFormAsync(null);

        Assert.False(choices.DatabaseConfigurationPreset);
        Assert.Equal("Default", form.TablePrefix);
    }

    [Fact]
    public async Task Choices_DatabaseProviderAndConnectionArePreset_HideDatabaseOptions()
    {
        var shellSettings = new ShellSettings();
        shellSettings["DatabaseProvider"] = DatabaseProviderValue.SqlConnection;
        shellSettings["ConnectionString"] = "Server=localhost;Database=Grove;";
        shellSettings["TablePrefix"] = "Default";

        var installation = CreateInstallation(shellSettings);

        var choices = await installation.GetChoicesAsync();
        var form = await installation.NewFormAsync(null);

        Assert.NotNull(choices.PresetDatabaseProvider);
        Assert.True(choices.DatabaseConfigurationPreset);
        Assert.Equal("Default", form.TablePrefix);
    }

    [Fact]
    public async Task Choices_DatabaseProviderIsConfigured_KeepsConnectionOptionsVisible()
    {
        var shellSettings = new ShellSettings();
        shellSettings["DatabaseProvider"] = DatabaseProviderValue.SqlConnection;

        var installation = CreateInstallation(shellSettings);

        var choices = await installation.GetChoicesAsync();
        var form = await installation.NewFormAsync(null);

        Assert.NotNull(choices.PresetDatabaseProvider);
        Assert.False(choices.DatabaseConfigurationPreset);
        Assert.Equal(DatabaseProviderValue.SqlConnection, form.DatabaseProvider);
    }

    [Fact]
    public async Task NewForm_Default_DefaultsSiteTimeZoneToSystemTimeZone()
    {
        var installation = CreateInstallation(new ShellSettings());

        var form = await installation.NewFormAsync(null);

        Assert.Equal("Europe/Paris", form.SiteTimeZone);
        Assert.Equal("SaaS", form.RecipeName);
    }

    [Fact]
    public async Task Install_DatabaseProviderIsConfigured_UsesPresetDatabaseProviderAndPostedConnectionString()
    {
        SetupContext capturedContext = null;
        var shellSettings = new ShellSettings();
        shellSettings["DatabaseProvider"] = DatabaseProviderValue.SqlConnection;

        var setupService = new Mock<ISetupService>();
        setupService
            .Setup(x => x.GetSetupRecipesAsync())
            .ReturnsAsync(
            [
                new RecipeDescriptor
                {
                    Name = "SaaS",
                    Tags = ["default"],
                },
            ]);
        setupService
            .Setup(x => x.SetupAsync(It.IsAny<SetupContext>()))
            .Callback<SetupContext>(context => capturedContext = context)
            .ReturnsAsync("execution-id");

        var installation = CreateInstallation(shellSettings, setupService.Object);

        var errors = await installation.InstallAsync(new SetupForm
        {
            SiteName = "Tenant",
            UserName = "admin",
            Email = "admin@test.com",
            Password = "Password1!",
            PasswordConfirmation = "Password1!",
            RecipeName = "SaaS",
            DatabaseProvider = DatabaseProviderValue.Sqlite,
            ConnectionString = "Server=localhost;Database=Tenant;",
            SiteTimeZone = "UTC",
        });

        Assert.Empty(errors);
        Assert.NotNull(capturedContext);
        Assert.Equal(DatabaseProviderValue.SqlConnection, capturedContext.Properties[SetupConstants.DatabaseProvider]);
        Assert.Equal("Server=localhost;Database=Tenant;", capturedContext.Properties[SetupConstants.DatabaseConnectionString]);
    }

    [Fact]
    public async Task Install_PasswordsDiffer_RefusesWithoutRunningSetup()
    {
        var setupService = new Mock<ISetupService>();
        setupService.Setup(x => x.GetSetupRecipesAsync()).ReturnsAsync([new RecipeDescriptor { Name = "SaaS", Tags = ["default"] }]);

        var installation = CreateInstallation(new ShellSettings(), setupService.Object);

        var errors = await installation.InstallAsync(new SetupForm
        {
            SiteName = "Tenant",
            UserName = "admin",
            Email = "admin@test.com",
            Password = "Password1!",
            PasswordConfirmation = "Other1!",
            RecipeName = "SaaS",
        });

        Assert.True(errors.ContainsKey(nameof(SetupForm.PasswordConfirmation)));
        setupService.Verify(x => x.SetupAsync(It.IsAny<SetupContext>()), Times.Never);
    }

    private static SetupInstallation CreateInstallation(ShellSettings shellSettings, ISetupService setupService = null)
    {
        var systemTimeZone = new Mock<ITimeZone>();
        systemTimeZone.SetupProperty(x => x.TimeZoneId, "Europe/Paris");

        var clock = new Mock<IClock>();
        clock
            .Setup(x => x.GetSystemTimeZone())
            .Returns(systemTimeZone.Object);

        if (setupService is null)
        {
            var setupServiceMock = new Mock<ISetupService>();
            setupServiceMock
                .Setup(x => x.GetSetupRecipesAsync())
                .ReturnsAsync([new RecipeDescriptor { Name = "SaaS", Tags = ["default"] }]);
            setupServiceMock
                .Setup(x => x.SetupAsync(It.IsAny<SetupContext>()))
                .ReturnsAsync("execution-id");

            setupService = setupServiceMock.Object;
        }

        var emailAddressValidator = new Mock<IEmailAddressValidator>();
        emailAddressValidator
            .Setup(x => x.Validate(It.IsAny<string>()))
            .Returns(true);

        var timeZones = new Mock<ITimeZoneSelectListProvider>();
        timeZones
            .Setup(x => x.GetTimeZoneSelectListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new KeyValuePair<string, string>("Europe/Paris", "Paris")]);

        var localizer = new Mock<IStringLocalizer<SetupInstallation>>();
        localizer.Setup(x => x[It.IsAny<string>()]).Returns((string name) => new LocalizedString(name, name));
        localizer.Setup(x => x[It.IsAny<string>(), It.IsAny<object[]>()]).Returns((string name, object[] _) => new LocalizedString(name, name));

        return new SetupInstallation(
            clock.Object,
            setupService,
            shellSettings,
            Mock.Of<IShellHost>(),
            Options.Create(new IdentityOptions()),
            emailAddressValidator.Object,
            [
                new DatabaseProvider
                {
                    Name = "Sqlite",
                    Value = DatabaseProviderValue.Sqlite,
                    IsDefault = true,
                },
                new DatabaseProvider
                {
                    Name = "Sql Server",
                    Value = DatabaseProviderValue.SqlConnection,
                    HasConnectionString = true,
                    HasTablePrefix = true,
                },
            ],
            timeZones.Object,
            localizer.Object,
            Mock.Of<ILogger<SetupInstallation>>());
    }
}
