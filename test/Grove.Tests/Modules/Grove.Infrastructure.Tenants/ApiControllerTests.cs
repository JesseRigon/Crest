using Grove.Data;
using Grove.Communications.Email;
using Grove.Environment.Shell;
using Grove.Data.Storage;
using Grove.Environment.Shell.Models;
using Grove.Environment.Shell.Removing;
using Grove.Infrastructure.Setup.Services;
using Grove.Infrastructure.Tenants;
using Grove.Infrastructure.Tenants.Api;
using Grove.Infrastructure.Tenants.Models;
using Grove.Infrastructure.Tenants.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Grove.Modules.Grove.Tenants.Tests;

public class ApiControllerTests
{
    private readonly Dictionary<string, ShellSettings> _shellSettings = [];
    private readonly Mock<IClock> _clockMock = new();
    private readonly Dictionary<string, FeatureProfile> _featureProfiles = new()
    {
        { "Feature Profile", new FeatureProfile() },
    };

    private delegate void TryGetSettingsCallback(string name, out ShellSettings settings);

    [Fact]
    public async Task CallCreateApiMultipleTimes_ReturnsSetupToken_CreatesTenant()
    {
        // Arrange
        var handlers = CreateHandlers();
        var viewModel = new TenantApiModel
        {
            Name = "Test",
            RequestUrlPrefix = "test",
            RequestUrlHost = "orchardcore.net",
            FeatureProfiles = ["Feature Profile"],
            IsNewTenant = true,
        };

        // Act & Assert
        var result = await handlers.CreateAsync(viewModel);

        Assert.IsType<ContentHttpResult>(result);

        var token1 = (result as ContentHttpResult).ResponseContent
            .ToString()
            .Split("token=")?[1];

        Assert.NotNull(token1);

        handlers = CreateHandlers();

        result = await handlers.CreateAsync(viewModel);

        Assert.IsType<Created<object>>(result);

        var token2 = (result as Created<object>).Location
            .ToString()
            .Split("token=")?[1];

        Assert.NotNull(token2);
        Assert.Equal(token1, token2);
    }

    [Fact]
    public async Task GetNewTenantSetupToken_ThePreviousTokenExpired_Succeeds()
    {
        // Arrange
        var handlers = CreateHandlers();
        var viewModel = new TenantApiModel
        {
            Name = "Test",
            RequestUrlPrefix = "test",
            RequestUrlHost = "orchardcore.net",
            FeatureProfiles = ["Feature Profile"],
            IsNewTenant = true,
        };

        // Act & Assert
        var result = await handlers.CreateAsync(viewModel);

        Assert.IsType<ContentHttpResult>(result);

        var token1 = (result as ContentHttpResult).ResponseContent
            .ToString()
            .Split("token=")?[1];

        Assert.NotNull(token1);

        // Enforce the setup token to be expired.
        _clockMock.Setup(clock => clock.UtcNow).Returns(DateTime.Now.AddDays(2));

        handlers = CreateHandlers();

        result = await handlers.CreateAsync(viewModel);

        Assert.IsType<Created<object>>(result);

        var token2 = (result as Created<object>).Location
            .ToString()
            .Split("token=")?[1];

        Assert.NotNull(token2);
        Assert.NotEqual(token1, token2);
    }

    [Fact]
    public async Task Create_Required_RejectssMissingTablePrefix()
    {
        // Arrange
        var handlers = CreateHandlers(requireTablePrefix: true);
        var viewModel = new TenantApiModel
        {
            Name = "Test",
            RequestUrlPrefix = "test",
            RequestUrlHost = "orchardcore.net",
            DatabaseProvider = DatabaseProviderValue.SqlConnection,
            TablePrefix = string.Empty,
            FeatureProfiles = ["Feature Profile"],
            IsNewTenant = true,
        };

        // Act
        var result = await handlers.CreateAsync(viewModel);

        // Assert
        var problem = Assert.IsType<ValidationProblem>(result);
        var error = Assert.Single(problem.ProblemDetails.Errors["tablePrefix"]);
        Assert.Equal("A table prefix is required.", error);
    }

    [Fact]
    public async Task Create_RequiredAndDatabaseProviderIsPreset_RejectssMissingTablePrefix()
    {
        // Arrange
        var handlers = CreateHandlers(
            requireTablePrefix: true,
            presetDatabaseProvider: DatabaseProviderValue.SqlConnection);

        var viewModel = new TenantApiModel
        {
            Name = "Test",
            RequestUrlPrefix = "test",
            RequestUrlHost = "orchardcore.net",
            TablePrefix = string.Empty,
            FeatureProfiles = ["Feature Profile"],
            IsNewTenant = true,
        };

        // Act
        var result = await handlers.CreateAsync(viewModel);

        // Assert
        var problem = Assert.IsType<ValidationProblem>(result);
        var error = Assert.Single(problem.ProblemDetails.Errors["tablePrefix"]);
        Assert.Equal("A table prefix is required.", error);
    }

    [Fact]
    public async Task Create_Configured_UsesPresetDatabaseConfiguration()
    {
        // Arrange
        const string presetConnectionString = "Server=localhost;Database=Grove;";

        var handlers = CreateHandlers(
            presetDatabaseProvider: DatabaseProviderValue.SqlConnection,
            presetConnectionString: presetConnectionString,
            presetSchema: "dbo");

        var viewModel = new TenantApiModel
        {
            Name = "Test",
            RequestUrlPrefix = "test",
            RequestUrlHost = "orchardcore.net",
            DatabaseProvider = DatabaseProviderValue.Sqlite,
            ConnectionString = "Ignored",
            Schema = "ignored",
            TablePrefix = "Test",
            FeatureProfiles = ["Feature Profile"],
            IsNewTenant = true,
        };

        // Act
        var result = await handlers.CreateAsync(viewModel);

        // Assert
        Assert.IsType<ContentHttpResult>(result);
        Assert.Equal(DatabaseProviderValue.SqlConnection, _shellSettings["Test"]["DatabaseProvider"]);
        Assert.Equal(presetConnectionString, _shellSettings["Test"]["ConnectionString"]);
        Assert.Equal("dbo", _shellSettings["Test"]["Schema"]);
    }

    [Fact]
    public async Task Create_LyProviderIsConfigured_UsesPresetDatabaseProviderAndPostedConnectionString()
    {
        // Arrange
        var handlers = CreateHandlers(
            presetDatabaseProvider: DatabaseProviderValue.SqlConnection);

        var viewModel = new TenantApiModel
        {
            Name = "Test",
            RequestUrlPrefix = "test",
            RequestUrlHost = "orchardcore.net",
            DatabaseProvider = DatabaseProviderValue.Sqlite,
            ConnectionString = "Server=localhost;Database=Tenant;",
            TablePrefix = "Test",
            FeatureProfiles = ["Feature Profile"],
            IsNewTenant = true,
        };

        // Act
        var result = await handlers.CreateAsync(viewModel);

        // Assert
        Assert.IsType<ContentHttpResult>(result);
        Assert.Equal(DatabaseProviderValue.SqlConnection, _shellSettings["Test"]["DatabaseProvider"]);
        Assert.Equal("Server=localhost;Database=Tenant;", _shellSettings["Test"]["ConnectionString"]);
    }

    [Fact]
    public async Task Edit_Configured_UsesPresetDatabaseConfiguration()
    {
        // Arrange
        const string presetConnectionString = "Server=localhost;Database=Grove;";

        _shellSettings["Test"] = new ShellSettings { Name = "Test", RequestUrlPrefix = "test" }.AsUninitialized();

        var handlers = CreateHandlers(
            presetDatabaseProvider: DatabaseProviderValue.SqlConnection,
            presetConnectionString: presetConnectionString,
            presetSchema: "dbo");

        var viewModel = new TenantApiModel
        {
            Name = "Test",
            RequestUrlPrefix = "test",
            RequestUrlHost = "orchardcore.net",
            DatabaseProvider = DatabaseProviderValue.Sqlite,
            ConnectionString = "Ignored",
            Schema = "ignored",
            TablePrefix = "Test",
            FeatureProfiles = ["Feature Profile"],
        };

        // Act
        var result = await handlers.EditAsync(viewModel);

        // Assert
        Assert.IsType<Ok>(result);
        Assert.Equal(DatabaseProviderValue.SqlConnection, _shellSettings["Test"]["DatabaseProvider"]);
        Assert.Equal(presetConnectionString, _shellSettings["Test"]["ConnectionString"]);
        Assert.Equal("dbo", _shellSettings["Test"]["Schema"]);
    }

    [Fact]
    public async Task Create_SavingTenantFails_ReturnsGenericError()
    {
        // Arrange
        var handlers = CreateHandlers(throwOnUpdate: true);
        var viewModel = new TenantApiModel
        {
            Name = "Test",
            RequestUrlPrefix = "test",
            RequestUrlHost = "orchardcore.net",
            TablePrefix = "Test",
            FeatureProfiles = ["Feature Profile"],
            IsNewTenant = true,
        };

        // Act
        var result = await handlers.CreateAsync(viewModel);

        // Assert
        var problem = Assert.IsType<ValidationProblem>(result);
        var error = Assert.Single(problem.ProblemDetails.Errors[string.Empty]);
        Assert.Equal("An error occurred while saving the tenant settings.", error);
    }

    [Fact]
    public async Task Create_Configured_UsesConfiguredTablePrefixPattern()
    {
        // Arrange
        var handlers = CreateHandlers(tablePrefixPattern: "{{ ShellSettings.Name }}", schemaPattern: "dbo");
        var viewModel = new TenantApiModel
        {
            Name = "PatternTenant",
            RequestUrlPrefix = "pattern-tenant",
            RequestUrlHost = "orchardcore.net",
            DatabaseProvider = DatabaseProviderValue.SqlConnection,
            TablePrefix = "Ignored",
            Schema = "ignored",
            FeatureProfiles = ["Feature Profile"],
            IsNewTenant = true,
        };

        // Act
        var result = await handlers.CreateAsync(viewModel);

        // Assert
        Assert.IsType<ContentHttpResult>(result);
        Assert.Equal("PatternTenant", _shellSettings["PatternTenant"]["TablePrefix"]);
        Assert.Equal("dbo", _shellSettings["PatternTenant"]["Schema"]);
    }

    [Fact]
    public async Task Create_Default_RejectsInvalidConfiguredTablePrefixPattern()
    {
        // Arrange
        var handlers = CreateHandlers(tablePrefixPattern: "tenant-{{ ShellSettings.Name }}");
        var viewModel = new TenantApiModel
        {
            Name = "PatternTenant",
            RequestUrlPrefix = "pattern-tenant",
            RequestUrlHost = "orchardcore.net",
            DatabaseProvider = DatabaseProviderValue.SqlConnection,
            FeatureProfiles = ["Feature Profile"],
            IsNewTenant = true,
        };

        // Act
        var result = await handlers.CreateAsync(viewModel);

        // Assert
        var problem = Assert.IsType<ValidationProblem>(result);
        var error = Assert.Single(problem.ProblemDetails.Errors["tablePrefix"]);
        Assert.Equal("The configured table prefix pattern must resolve to a valid SQL identifier using only letters, numbers, and underscores, and it must start with a letter or underscore.", error);
    }

    private TenantApiHandlers CreateHandlers(
        bool requireTablePrefix = false,
        string presetDatabaseProvider = null,
        string presetConnectionString = null,
        string presetSchema = null,
        string tablePrefixPattern = null,
        string schemaPattern = null,
        bool throwOnUpdate = false)
    {
        var defaultShellSettings = new ShellSettings()
            .AsDefaultShell()
            .AsRunning();

        defaultShellSettings["DatabaseProvider"] = presetDatabaseProvider;
        defaultShellSettings["ConnectionString"] = presetConnectionString;
        defaultShellSettings["Schema"] = presetSchema;

        var shellHostMock = new Mock<IShellHost>();
        shellHostMock
            .Setup(host => host.UpdateShellSettingsAsync(It.IsAny<ShellSettings>()))
            .Callback<ShellSettings>(settings =>
            {
                if (throwOnUpdate)
                {
                    throw new InvalidOperationException("Hidden database details should not leak.");
                }

                _shellSettings[settings.Name] = settings;
            });

        var _ = It.IsAny<ShellSettings>();
        shellHostMock
            .Setup(host => host.TryGetSettings(It.IsAny<string>(), out _))
            .Callback(new TryGetSettingsCallback((string name, out ShellSettings settings) => _shellSettings.TryGetValue(name, out settings)))
            .Returns<string, ShellSettings>((name, _) => _shellSettings.ContainsKey(name));

        var shellSettingsManagerMock = new Mock<IShellSettingsManager>();
        shellSettingsManagerMock
            .Setup(shellSettingsManager => shellSettingsManager.CreateDefaultSettings())
            .Returns(defaultShellSettings);

        var dataProtectionProviderMock = new Mock<IDataProtectionProvider>();
        dataProtectionProviderMock
            .Setup(dataProtectionProvider => dataProtectionProvider.CreateProtector(It.IsAny<string>()))
            .Returns(new FakeDataProtector());

        var authorizeServiceMock = new Mock<IAuthorizationService>();
        authorizeServiceMock
            .Setup(authorizeService => authorizeService.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<object>(),
                It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success);

        var featureProfilesService = new Mock<IFeatureProfilesService>();
        featureProfilesService
            .Setup(featureProfiles => featureProfiles.GetFeatureProfilesAsync())
            .ReturnsAsync(_featureProfiles);

        var databaseProviders = new[]
        {
            new DatabaseProvider
            {
                Name = "Sql Server",
                Value = DatabaseProviderValue.SqlConnection,
                HasConnectionString = true,
                HasTablePrefix = true,
            },
            new DatabaseProvider
            {
                Name = "Sqlite",
                Value = DatabaseProviderValue.Sqlite,
                HasTablePrefix = false,
            },
        };

        var stringLocalizerMock = new Mock<IStringLocalizer<TenantValidator>>();
        stringLocalizerMock
            .Setup(localizer => localizer[It.IsAny<string>()])
            .Returns<string>(name => new LocalizedString(name, name));
        stringLocalizerMock
            .Setup(localizer => localizer[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns<string, object[]>((name, args) => new LocalizedString(name, string.Format(CultureInfo.InvariantCulture, name, args)));

        var tenantApiLocalizerMock = new Mock<IStringLocalizer<TenantApiHandlers>>();
        tenantApiLocalizerMock
            .Setup(localizer => localizer[It.IsAny<string>()])
            .Returns<string>(name => new LocalizedString(name, name));
        tenantApiLocalizerMock
            .Setup(localizer => localizer[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns<string, object[]>((name, args) => new LocalizedString(name, string.Format(CultureInfo.InvariantCulture, name, args)));

        var patternLocalizerMock = new Mock<IStringLocalizer<TenantDatabasePatternResolver>>();
        patternLocalizerMock
            .Setup(localizer => localizer[It.IsAny<string>()])
            .Returns<string>(name => new LocalizedString(name, name));
        patternLocalizerMock
            .Setup(localizer => localizer[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns<string, object[]>((name, args) => new LocalizedString(name, string.Format(CultureInfo.InvariantCulture, name, args)));

        var tenantOptions = new TenantsOptions
        {
            RequireTablePrefix = requireTablePrefix,
            TablePrefixPattern = tablePrefixPattern,
            SchemaPattern = schemaPattern,
        };

        var tenantDatabasePatternResolver = new TenantDatabasePatternResolver(
            new Fluid.FluidParser(),
            Options.Create(tenantOptions),
            patternLocalizerMock.Object);

        var tenantValidator = new TenantValidator(
            shellHostMock.Object,
            shellSettingsManagerMock.Object,
            featureProfilesService.Object,
            Mock.Of<IDbConnectionValidator>(),
            Options.Create(tenantOptions),
            databaseProviders,
            tenantDatabasePatternResolver,
            stringLocalizerMock.Object);

        var handlers = new TenantApiHandlers(
            shellHostMock.Object,
            defaultShellSettings,
            Mock.Of<IShellRemovalManager>(),
            authorizeServiceMock.Object,
            shellSettingsManagerMock.Object,
            dataProtectionProviderMock.Object,
            _clockMock.Object,
            Options.Create(tenantOptions),
            databaseProviders,
            tenantValidator,
            tenantDatabasePatternResolver,
            tenantApiLocalizerMock.Object,
            Mock.Of<ILogger<TenantApiHandlers>>());

        handlers.BindTo(CreateHttpContext());
        return handlers;
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(),
        };

        httpContext.Features.Set(new ShellContextFeature
        {
            OriginalPathBase = PathString.Empty,
        });

        httpContext.User.AddIdentity(new ClaimsIdentity());

        return httpContext;
    }

    private sealed class FakeDataProtector : IDataProtector
    {
        public IDataProtector CreateProtector(string purpose) => this;

        public byte[] Protect(byte[] plaintext) => plaintext;

        public byte[] Unprotect(byte[] protectedData) => protectedData;
    }
}
