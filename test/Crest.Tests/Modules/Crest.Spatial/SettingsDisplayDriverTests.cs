using Crest.DisplayManagement;
using Crest.DisplayManagement.Descriptors;
using Crest.DisplayManagement.Implementation;
using Crest.DisplayManagement.Theming;
using Crest.Environment.Extensions;
using Crest.Spatial.Drivers;
using Crest.Spatial.Fields;
using Crest.Spatial.Settings;
using Crest.Tests.Modules.Crest.ContentFields.Settings;
using Crest.Tests.Stubs;

namespace Crest.Tests.Modules.Crest.Spatial;

public class SettingsDisplayDriverTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IShapeFactory _shapeFactory;
    private readonly ShapeTable _shapeTable;

    public SettingsDisplayDriverTests()
    {
        var serviceCollection = new ServiceCollection();

        serviceCollection.AddScoped<ILoggerFactory, NullLoggerFactory>()
            .AddScoped<IThemeManager, ThemeManager>()
            .AddScoped<IShapeFactory, DefaultShapeFactory>()
            .AddScoped<IExtensionManager, StubExtensionManager>()
            .AddScoped<IShapeTableManager, TestShapeTableManager>();

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
    public async Task GeoPointFieldSettings_Default_Deserialize()
    {
        var settings = new GeoPointFieldSettings
        {
            Hint = "Test Hint",
            Required = true,
        };

        var contentDefinition = DisplayDriverTestHelper.GetContentPartDefinition<GeoPointField>(field => field.WithSettings(settings));

        var shapeResult = await DisplayDriverTestHelper.GetShapeResultAsync<GeoPointFieldSettingsDriver>(_shapeFactory, contentDefinition);

        var shape = (GeoPointFieldSettings)shapeResult.Shape;

        Assert.Equal(settings.Hint, shape.Hint);
        Assert.Equal(settings.Required, shape.Required);
    }
}
