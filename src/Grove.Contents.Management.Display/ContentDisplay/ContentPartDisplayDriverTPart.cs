using Grove.Contents.Management.Display.Models;
using Grove.Contents.Management.Metadata.Models;
using Grove.Display.Management;
using Grove.Display.Management.Handlers;
using Grove.Display.Management.Views;

namespace Grove.Contents.Management.Display.ContentDisplay;

/// <summary>
/// Any concrete implementation of this class can provide shapes for any content item which has a specific Part.
/// </summary>
/// <typeparam name="TPart"></typeparam>
public abstract class ContentPartDisplayDriver<TPart> : DisplayDriverBase, IContentPartDisplayDriver where TPart : ContentPart, new()
{
    private const string DisplaySeparator = "_Display__";

    private ContentTypePartDefinition _typePartDefinition;

    public override ShapeResult Factory<TBuilderState, TInitState>(string shapeType, Func<IBuildShapeContext, TBuilderState, ValueTask<IShape>> shapeBuilder, TBuilderState shapeBuilderState, Func<IShape, TInitState, ValueTask> initializingAsync, TInitState initializingState)
    {
        // e.g., HtmlBodyPart.Summary, HtmlBodyPart-BlogPost, BagPart-LandingPage-Services
        // context.Shape is the ContentItem shape, we need to alter the part shape

        var result = base.Factory(shapeType, shapeBuilder, shapeBuilderState, initializingAsync, initializingState);

        if (_typePartDefinition != null)
        {
            var partName = _typePartDefinition.Name;
            var partType = _typePartDefinition.PartDefinition.Name;
            var displayMode = _typePartDefinition.DisplayMode();
            var hasDisplayMode = !string.IsNullOrEmpty(displayMode);
            var isDisplayModeShapeType = shapeType == partType + DisplaySeparator + displayMode;

            // If the shape type and the field type only differ by the display mode
            if (hasDisplayMode && isDisplayModeShapeType)
            {
                // Preserve the shape name regardless its differentiator
                result.Name(partName);
            }

            if (partType == shapeType || isDisplayModeShapeType)
            {
                // HtmlBodyPart, Services
                result.Differentiator(partName);
            }
            else
            {
                // ListPart-ListPartFeed
                result.Differentiator($"{partName}-{shapeType}");
            }

            // The type part definition will be cleared before the displaying event is raised, so
            // we need to cache it here for use in the displaying event to add alternates.
            var typePartDefinition = _typePartDefinition;
            result.Displaying(ctx =>
            {
                var displayType = ctx.Shape.Metadata.DisplayType;

                // Get cached alternates for this display type and add them efficiently
                var cachedAlternates = ContentPartAlternatesFactory.GetAlternates(typePartDefinition, shapeType, displayType);
                ctx.Shape.Metadata.Alternates.AddRange(cachedAlternates);
            });
        }

        return result;
    }

    async Task<IDisplayResult> IContentPartDisplayDriver.BuildDisplayAsync(ContentPart contentPart, ContentTypePartDefinition typePartDefinition, BuildDisplayContext context)
    {
        var part = contentPart as TPart;

        if (part == null)
        {
            return null;
        }

        using (BuildPrefix(typePartDefinition, context.HtmlFieldPrefix))
        {
            _typePartDefinition = typePartDefinition;

            var buildDisplayContext = new BuildPartDisplayContext(typePartDefinition, context);

            var result = await DisplayAsync(part, buildDisplayContext);

            _typePartDefinition = null;

            return result;
        }
    }

    public virtual Task<IDisplayResult> DisplayAsync(TPart part, BuildPartDisplayContext context)
    {
        return Task.FromResult(Display(part, context));
    }

    public virtual IDisplayResult Display(TPart part, BuildPartDisplayContext context)
    {
        return null;
    }

    protected string GetDisplayShapeType(string shapeType, BuildPartDisplayContext context)
    {
        var displayMode = context.TypePartDefinition.DisplayMode();
        return !string.IsNullOrEmpty(displayMode)
            ? shapeType + DisplaySeparator + displayMode
            : shapeType;
    }

    protected string GetDisplayShapeType(BuildPartDisplayContext context)
    {
        return GetDisplayShapeType(typeof(TPart).Name, context);
    }

    private TempPrefix BuildPrefix(ContentTypePartDefinition typePartDefinition, string htmlFieldPrefix)
    {
        var tempPrefix = new TempPrefix(this, Prefix);

        Prefix = typePartDefinition.Name;

        if (!string.IsNullOrEmpty(htmlFieldPrefix))
        {
            Prefix = htmlFieldPrefix + "." + Prefix;
        }

        return tempPrefix;
    }

    /// <summary>
    /// Restores the previous prefix automatically.
    /// </summary>
    private sealed class TempPrefix : IDisposable
    {
        private readonly ContentPartDisplayDriver<TPart> _driver;
        private readonly string _originalPrefix;

        public TempPrefix(ContentPartDisplayDriver<TPart> driver, string originalPrefix)
        {
            _driver = driver;
            _originalPrefix = originalPrefix;
        }

        public void Dispose()
        {
            _driver.Prefix = _originalPrefix;
        }
    }
}
