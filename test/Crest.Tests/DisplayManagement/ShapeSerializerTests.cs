using Crest.DisplayManagement;
using Crest.DisplayManagement.Descriptors;
using Crest.DisplayManagement.Implementation;
using Crest.DisplayManagement.Theming;
using Crest.Environment.Extensions;
using Crest.Tests.Stubs;
using Arguments = Crest.DisplayManagement.Arguments;

namespace Crest.Tests.DisplayManagement;

public class ShapeSerializerTests
{
    private readonly IServiceProvider _serviceProvider;

    public ShapeSerializerTests()
    {
        IServiceCollection serviceCollection = new ServiceCollection();

        serviceCollection.AddLogging();
        serviceCollection.AddScoped<IHtmlDisplay, DefaultHtmlDisplay>();
        serviceCollection.AddScoped<IExtensionManager, StubExtensionManager>();
        serviceCollection.AddScoped<IThemeManager, ThemeManager>();
        serviceCollection.AddScoped<IShapeFactory, DefaultShapeFactory>();
        serviceCollection.AddScoped<IShapeTableManager, TestShapeTableManager>();

        var defaultShapeTable = new ShapeTable
        (
            new Dictionary<string, ShapeDescriptor>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, ShapeBinding>(StringComparer.OrdinalIgnoreCase)
        );

        serviceCollection.AddSingleton(defaultShapeTable);

        _serviceProvider = serviceCollection.BuildServiceProvider();
    }

    [Fact]
    public async Task Serialize_Default_Succeeds()
    {
        var shape = _serviceProvider.GetService<IShapeFactory>();

        var alpha = await shape.CreateAsync("Alpha");
        var serialized = alpha.ShapeToJson();

        Assert.Contains("Alpha", serialized.ToString());
    }

    [Fact]
    public async Task Skip_RecursiveShapes_Succeeds()
    {
        var shape = _serviceProvider.GetService<IShapeFactory>();

        var alpha = await shape.CreateAsync("Alpha");

        var beta = await shape.CreateAsync("Beta", Arguments.From(new
        {
            Alpha = alpha,
        }));

        await alpha.AddAsync(beta);
        var serialized = alpha.ShapeToJson();

        Assert.Contains("Beta", serialized.ToString());
    }
}
