using Microsoft.Extensions.Logging;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Contents.Management.Display.ViewModels;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents.Management.Metadata.Settings;
using Grove.Display.Management;
using Grove.Display.Management.Handlers;
using Grove.Display.Management.Views;
using Grove.Display.Management.Zones;
using Grove.Modules;

namespace Grove.Contents.Management.Display;

/// <summary>
/// Provides a concrete implementation of a display handler coordinating part, field and content item drivers.
/// </summary>
public class ContentItemDisplayCoordinator : IContentDisplayHandler
{
    private readonly IContentPartDisplayDriverResolver _contentPartDisplayDriverResolver;
    private readonly IContentFieldDisplayDriverResolver _contentFieldDisplayDriverResolver;
    private readonly IEnumerable<IContentDisplayDriver> _displayDrivers;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ITypeActivatorFactory<ContentPart> _contentPartFactory;
    private readonly ILogger _logger;

    public ContentItemDisplayCoordinator(
        IContentPartDisplayDriverResolver contentPartDisplayDriverResolver,
        IContentFieldDisplayDriverResolver contentFieldDisplayDriverResolver,
        IContentDefinitionManager contentDefinitionManager,
        IEnumerable<IContentDisplayDriver> displayDrivers,
        ITypeActivatorFactory<ContentPart> contentPartFactory,
        ILogger<ContentItemDisplayCoordinator> logger)
    {
        _contentPartDisplayDriverResolver = contentPartDisplayDriverResolver;
        _contentFieldDisplayDriverResolver = contentFieldDisplayDriverResolver;
        _contentPartFactory = contentPartFactory;
        _contentDefinitionManager = contentDefinitionManager;
        _displayDrivers = displayDrivers;
        _logger = logger;
    }

    public async Task BuildDisplayAsync(ContentItem contentItem, BuildDisplayContext context)
    {
        var contentTypeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentItem.ContentType);

        if (contentTypeDefinition == null)
        {
            return;
        }

        foreach (var displayDriver in _displayDrivers)
        {
            try
            {
                var result = await displayDriver.BuildDisplayAsync(contentItem, context);
                if (result != null)
                {
                    await result.ApplyAsync(context);
                }
            }
            catch (Exception ex) when (!ex.IsFatal())
            {
                ex.LogException(_logger, displayDriver.GetType(), nameof(BuildDisplayAsync));
            }
        }

        var hasStereotype = contentTypeDefinition.TryGetStereotype(out var stereotype);

        foreach (var contentTypePartDefinition in contentTypeDefinition.Parts)
        {
            var partName = contentTypePartDefinition.Name;
            var partTypeName = contentTypePartDefinition.PartDefinition.Name;
            var partActivator = _contentPartFactory.GetTypeActivator(partTypeName);
            var part = contentItem.Get(partActivator.Type, partName) as ContentPart;

            if (part == null)
            {
                continue;
            }

            var contentType = contentTypePartDefinition.ContentTypeDefinition.Name;
            var partDisplayDrivers = _contentPartDisplayDriverResolver.GetDisplayModeDrivers(partTypeName, contentTypePartDefinition.DisplayMode());
            foreach (var partDisplayDriver in partDisplayDrivers)
            {
                try
                {
                    var result = await partDisplayDriver.BuildDisplayAsync(part, contentTypePartDefinition, context);
                    if (result != null)
                    {
                        await result.ApplyAsync(context);
                    }
                }
                catch (Exception ex) when (!ex.IsFatal())
                {
                    ex.LogException(_logger, partDisplayDrivers.GetType(), nameof(BuildDisplayAsync));
                }
            }
            var tempContext = context;

            // Create a custom ContentPart shape that will hold the fields for dynamic content part (not implicit parts)
            // This allows its fields to be grouped and templated

            if (part.GetType() == typeof(ContentPart) && partTypeName != contentTypePartDefinition.ContentTypeDefinition.Name)
            {
                var shapeType = context.DisplayType != PlatformConstants.DisplayType.Detail ? "ContentPart_" + context.DisplayType : "ContentPart";

                var shapeResult = ShapeResult.Create(
                    shapeType,
                    static (ctx, buildShapeType) => ctx.ShapeFactory.CreateAsync(
                        buildShapeType,
                        static shapeContext =>
                            ValueTask.FromResult<IShape>(
                                new ZoneHolding<IShapeFactory>(
                                    static factory => factory.CreateAsync("Zone"),
                                    shapeContext.ShapeFactory)),
                        ctx),
                    shapeType);

                shapeResult.Differentiator(partName);
                shapeResult.Name(partName);
                shapeResult.Location("Content");
                shapeResult.OnGroup(context.GroupId);
                shapeResult.Displaying(ctx =>
                {
                    var displayType = ctx.Shape.Metadata.DisplayType;

                    // Get cached alternates and add them efficiently
                    var cachedAlternates = ContentPartShapeAlternatesFactory.GetDisplayAlternates(
                        contentType,
                        partTypeName,
                        partName,
                        stereotype,
                        hasStereotype,
                        displayType);

                    ctx.Shape.Metadata.Alternates.AddRange(cachedAlternates);
                });

                await shapeResult.ApplyAsync(context);

                var contentPartShape = shapeResult.Shape;

                if (contentPartShape == null)
                {
                    // Part is explicitly hidden in placement.
                    continue;
                }

                // Make the ContentPart property available on the shape
                contentPartShape.Properties[partTypeName] = part.Content;
                contentPartShape.Properties["ContentItem"] = part.ContentItem;

                context = new BuildDisplayContext(shapeResult.Shape, context.DisplayType, context.GroupId, context.ShapeFactory, context.Layout)
                {
                    // With a new display context we have the default FindPlacementDelegate that returns null, so we reuse the delegate from the temp context.
                    FindPlacement = tempContext.FindPlacement,
                };
            }

            foreach (var contentPartFieldDefinition in contentTypePartDefinition.PartDefinition.Fields)
            {
                var fieldDisplayDrivers = _contentFieldDisplayDriverResolver.GetDisplayModeDrivers(contentPartFieldDefinition.FieldDefinition.Name, contentPartFieldDefinition.DisplayMode());
                foreach (var fieldDisplayDriver in fieldDisplayDrivers)
                {
                    try
                    {
                        var result = await fieldDisplayDriver.BuildDisplayAsync(part, contentPartFieldDefinition, contentTypePartDefinition, context);
                        if (result != null)
                        {
                            await result.ApplyAsync(context);
                        }
                    }
                    catch (Exception ex) when (!ex.IsFatal())
                    {
                        ex.LogException(_logger, fieldDisplayDriver.GetType(), nameof(BuildDisplayAsync));
                    }
                }
            }

            context = tempContext;
        }
    }
}

