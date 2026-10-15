using Microsoft.Extensions.Logging;
using Grove.Display.Management.Descriptors;
using Grove.Display.Management.Handlers;
using Grove.Display.Management.Layout;
using Grove.Modules;

namespace Grove.Display.Management;

public class DisplayManager<TModel> : BaseDisplayManager, IDisplayManager<TModel>
{
    private readonly IEnumerable<IDisplayDriver<TModel>> _drivers;
    private readonly IShapeFactory _shapeFactory;
    private readonly ILayoutAccessor _layoutAccessor;
    private readonly ILogger _logger;

    public DisplayManager(
        IEnumerable<IDisplayDriver<TModel>> drivers,
        IShapeFactory shapeFactory,
        IEnumerable<IShapePlacementProvider> placementProviders,
        ILogger<DisplayManager<TModel>> logger,
        ILayoutAccessor layoutAccessor
        ) : base(shapeFactory, placementProviders)
    {
        _shapeFactory = shapeFactory;
        _layoutAccessor = layoutAccessor;
        _drivers = drivers;
        _logger = logger;
    }

    public async Task<IShape> BuildDisplayAsync(TModel model, string displayType = null, string group = null)
    {
        var actualShapeType = typeof(TModel).Name;
        var modelTypeName = model.GetType().Name;

        var actualDisplayType = string.IsNullOrEmpty(displayType) ? PlatformConstants.DisplayType.Detail : displayType;

        // _[DisplayType] is only added for the ones different than Detail
        if (actualDisplayType != PlatformConstants.DisplayType.Detail)
        {
            actualShapeType = actualShapeType + "_" + actualDisplayType;
        }

        var shape = await CreateContentShapeAsync(actualShapeType);

        // This provides a way to default a safe default and customize for each model type
        shape.Metadata.Alternates.AddRange(DisplayManagerAlternatesFactory.GetDisplayAlternates(actualShapeType, modelTypeName));

        var context = new BuildDisplayContext(
            shape,
            actualDisplayType,
            group ?? "",
            _shapeFactory,
            await _layoutAccessor.GetLayoutAsync()
        );

        await BindPlacementAsync(context);

        await _drivers.InvokeAsync(async (driver, model, context) =>
        {
            var result = await driver.BuildDisplayAsync(model, context);
            if (result != null)
            {
                await result.ApplyAsync(context);
            }
        }, model, context, _logger);

        return shape;
    }
}
