using Crest.ContentFields.Fields;
using Crest.DisplayManagement;
using Crest.DisplayManagement.Descriptors;
using Crest.DisplayManagement.Implementation;
using Crest.DisplayManagement.Theming;
using Crest.Environment.Extensions;
using Crest.Environment.Shell;
using Crest.Environment.Shell.Builders;
using Crest.Environment.Shell.Scope;
using Crest.Indexing;
using Crest.Indexing.Models;
using Crest.Locking;
using Crest.Locking.Distributed;
using Crest.Recipes.Services;
using Crest.Scripting;
using Crest.Elasticsearch.Core.Models;
using Crest.Elasticsearch.Drivers;
using Crest.Tests.Modules.Crest.ContentFields.Settings;
using Crest.Tests.Stubs;

namespace Crest.Tests.Modules.Crest.Elasticsearch;

public partial class SettingsDisplayDriverTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IShapeFactory _shapeFactory;
    private readonly ShapeTable _shapeTable;

    public SettingsDisplayDriverTests()
    {
        var serviceCollection = new ServiceCollection();

        serviceCollection.AddScripting()
            .AddLogging()
            .AddScoped<ILoggerFactory, NullLoggerFactory>()
            .AddScoped<IThemeManager, ThemeManager>()
            .AddScoped<IShapeFactory, DefaultShapeFactory>()
            .AddScoped<IExtensionManager, StubExtensionManager>()
            .AddScoped<IShapeTableManager, TestShapeTableManager>()
            .AddSingleton<IDistributedLock, LocalLock>();

        _shapeTable = new ShapeTable
        (
            new Dictionary<string, ShapeDescriptor>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, ShapeBinding>(StringComparer.OrdinalIgnoreCase)
        );

        serviceCollection.AddSingleton(_shapeTable);

        _serviceProvider = serviceCollection.BuildServiceProvider();

        _shapeFactory = _serviceProvider.GetRequiredService<IShapeFactory>();
    }

    [Fact]
    public async Task ContentPickerFieldElasticEditorSettings_Default_Deserialize()
    {

        await (await CreateShellContext().CreateScopeAsync()).UsingAsync(async scope =>
        {
            // Arrange
            var shellHostMock = new Mock<IShellHost>();

            shellHostMock.Setup(h => h.GetScopeAsync(It.IsAny<ShellSettings>()))
                .Returns(GetScopeAsync);

            var loggerMock = new Mock<ILogger<RecipeExecutor>>();
            var localizerMock = new Mock<IStringLocalizer<RecipeExecutor>>();

            localizerMock.Setup(localizer => localizer[It.IsAny<string>()])
            .Returns((string name) => new LocalizedString(name, name));

            var settings = new ContentPickerFieldElasticEditorSettings
            {
                Index = "testIndex",
                Indices = ["idx1", "idx2", "testIndex"],
            };

            var indexes = new List<IndexProfile>
            {
                new IndexProfile
                {
                    Id = "idx1",
                    IndexName = "idx1",
                    IndexFullName = "idx1",
                    ProviderName = "elasticsearch",
                    Type = "Content",
                    Name = "Test Index",
                },
                new IndexProfile
                {
                    Id = "idx2",
                    IndexName = "idx2",
                    IndexFullName = "idx2",
                    ProviderName = "elasticsearch",
                    Type = "Content",
                    Name = "Test Index",
                },
                new IndexProfile
                {
                    Id = "testIndex",
                    IndexName = "testIndex",
                    IndexFullName = "testIndex",
                    ProviderName = "elasticsearch",
                    Type = "Content",
                    Name = "Test Index",
                },
            };

            var contentDefinition = DisplayDriverTestHelper.GetContentPartDefinition<ContentPickerField>(field => field.WithSettings(settings));
            var storeMock = new Mock<IIndexProfileStore>();
            storeMock.Setup(x => x.GetByProviderAsync(It.IsAny<string>()))
                .ReturnsAsync(indexes);

            // Act
            var shapeResult = await DisplayDriverTestHelper.GetShapeResultAsync(_shapeFactory, contentDefinition, new ContentPickerFieldElasticEditorSettingsDriver(storeMock.Object));
            var shape = (ContentPickerFieldElasticEditorSettings)shapeResult.Shape;

            // Assert
            Assert.Equal(settings.Index, shape.Index);
            Assert.Equal(settings.Indices, shape.Indices);
        });
    }

    private ShellContext CreateShellContext() => new()
    {
        Settings = new ShellSettings().AsDefaultShell().AsRunning(),
        ServiceProvider = _serviceProvider,
    };

    private static Task<ShellScope> GetScopeAsync()
        => ShellScope.Context.CreateScopeAsync();
}
