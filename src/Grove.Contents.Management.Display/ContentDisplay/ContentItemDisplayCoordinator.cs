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
    private readonly IContentFieldSetDisplayDriverResolver _contentFieldSetDisplayDriverResolver;
    private readonly IContentFieldDisplayDriverResolver _contentFieldDisplayDriverResolver;
    private readonly IEnumerable<IContentDisplayDriver> _displayDrivers;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ITypeActivatorFactory<ContentFieldSet> _contentFieldSetFactory;
    private readonly ILogger _logger;

    public ContentItemDisplayCoordinator(
        IContentFieldSetDisplayDriverResolver contentFieldSetDisplayDriverResolver,
        IContentFieldDisplayDriverResolver contentFieldDisplayDriverResolver,
        IContentDefinitionManager contentDefinitionManager,
        IEnumerable<IContentDisplayDriver> displayDrivers,
        ITypeActivatorFactory<ContentFieldSet> contentFieldSetFactory,
        ILogger<ContentItemDisplayCoordinator> logger)
    {
        _contentFieldSetDisplayDriverResolver = contentFieldSetDisplayDriverResolver;
        _contentFieldDisplayDriverResolver = contentFieldDisplayDriverResolver;
        _contentFieldSetFactory = contentFieldSetFactory;
        _contentDefinitionManager = contentDefinitionManager;
        _displayDrivers = displayDrivers;
        _logger = logger;
    }

    public async Task BuildDisplayAsync(ContentItem contentItem, BuildDisplayContext context)
    {
        // The definition the display was built with (a frozen item's stored schema), else the current one.
        var contentTypeDefinition = await ContentDisplayDefinitions.DefinitionAsync(context.Shape, contentItem, _contentDefinitionManager);

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

        foreach (var contentTypeFieldSetDefinition in contentTypeDefinition.FieldSets)
        {
            var partName = contentTypeFieldSetDefinition.Name;
            var fieldSetTypeName = contentTypeFieldSetDefinition.FieldSetDefinition.Name;
            var partActivator = _contentFieldSetFactory.GetTypeActivator(fieldSetTypeName);
            var part = contentItem.Get(partActivator.Type, partName) as ContentFieldSet;

            if (part == null)
            {
                continue;
            }

            var contentType = contentTypeFieldSetDefinition.ContentTypeDefinition.Name;
            var partDisplayDrivers = _contentFieldSetDisplayDriverResolver.GetDisplayModeDrivers(fieldSetTypeName, contentTypeFieldSetDefinition.DisplayMode());
            foreach (var partDisplayDriver in partDisplayDrivers)
            {
                try
                {
                    var result = await partDisplayDriver.BuildDisplayAsync(part, contentTypeFieldSetDefinition, context);
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

            // Create a custom ContentFieldSet shape that will hold the fields for dynamic field set (not implicit parts)
            // This allows its fields to be grouped and templated

            if (part.GetType() == typeof(ContentFieldSet) && fieldSetTypeName != contentTypeFieldSetDefinition.ContentTypeDefinition.Name)
            {
                var shapeType = context.DisplayType != PlatformConstants.DisplayType.Detail ? "ContentFieldSet_" + context.DisplayType : "ContentFieldSet";

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
                    var cachedAlternates = ContentFieldSetShapeAlternatesFactory.GetDisplayAlternates(
                        contentType,
                        fieldSetTypeName,
                        partName,
                        stereotype,
                        hasStereotype,
                        displayType);

                    ctx.Shape.Metadata.Alternates.AddRange(cachedAlternates);
                });

                await shapeResult.ApplyAsync(context);

                var contentFieldSetShape = shapeResult.Shape;

                if (contentFieldSetShape == null)
                {
                    // Part is explicitly hidden in placement.
                    continue;
                }

                // Make the ContentFieldSet property available on the shape
                contentFieldSetShape.Properties[fieldSetTypeName] = part.Content;
                contentFieldSetShape.Properties["ContentItem"] = part.ContentItem;

                context = new BuildDisplayContext(shapeResult.Shape, context.DisplayType, context.GroupId, context.ShapeFactory, context.Layout)
                {
                    // With a new display context we have the default FindPlacementDelegate that returns null, so we reuse the delegate from the temp context.
                    FindPlacement = tempContext.FindPlacement,
                };
            }

            foreach (var contentFieldDefinition in contentTypeFieldSetDefinition.FieldSetDefinition.Fields)
            {
                // A field its field set's display shows, or that a module keeps, has no display of its own.
                if (string.Equals(contentFieldDefinition.DisplayMode(), Grove.Contents.Management.Metadata.Settings.ContentFieldDisplayModes.None, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var fieldDisplayDrivers = _contentFieldDisplayDriverResolver.GetDisplayModeDrivers(contentFieldDefinition.FieldTypeDefinition.Name, contentFieldDefinition.DisplayMode());
                foreach (var fieldDisplayDriver in fieldDisplayDrivers)
                {
                    try
                    {
                        var result = await fieldDisplayDriver.BuildDisplayAsync(part, contentFieldDefinition, contentTypeFieldSetDefinition, context);
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

