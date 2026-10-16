using Grove.Contents.Management.Display.Models;
using Grove.Contents.Management.Metadata.Models;
using Grove.Display.Management;
using Grove.Display.Management.Handlers;
using Grove.Display.Management.Views;

namespace Grove.Contents.Management.Display.ContentDisplay;

/// <summary>
/// Any concrete implementation of this class can provide shapes for any content item which has a specific Part.
/// </summary>
/// <typeparam name="TFieldSet"></typeparam>
public abstract class ContentFieldSetDisplayDriver<TFieldSet> : DisplayDriverBase, IContentFieldSetDisplayDriver where TFieldSet : ContentFieldSet, new()
{
    private const string DisplaySeparator = "_Display__";

    private ContentTypeFieldSetDefinition _typeFieldSetDefinition;

    public override ShapeResult Factory<TBuilderState, TInitState>(string shapeType, Func<IBuildShapeContext, TBuilderState, ValueTask<IShape>> shapeBuilder, TBuilderState shapeBuilderState, Func<IShape, TInitState, ValueTask> initializingAsync, TInitState initializingState)
    {
        // e.g., HtmlBodyFieldSet.Summary, HtmlBodyFieldSet-BlogPost, Taxonomy-LandingPage-Categories
        // context.Shape is the ContentItem shape, we need to alter the part shape

        var result = base.Factory(shapeType, shapeBuilder, shapeBuilderState, initializingAsync, initializingState);

        if (_typeFieldSetDefinition != null)
        {
            var partName = _typeFieldSetDefinition.Name;
            var partType = _typeFieldSetDefinition.FieldSetDefinition.Name;
            var displayMode = _typeFieldSetDefinition.DisplayMode();
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
                // HtmlBodyFieldSet, Services
                result.Differentiator(partName);
            }
            else
            {
                // ChildContentItemsFieldSet-ChildContentItemsFieldSetFeed
                result.Differentiator($"{partName}-{shapeType}");
            }

            // The type part definition will be cleared before the displaying event is raised, so
            // we need to cache it here for use in the displaying event to add alternates.
            var typeFieldSetDefinition = _typeFieldSetDefinition;
            result.Displaying(ctx =>
            {
                var displayType = ctx.Shape.Metadata.DisplayType;

                // Get cached alternates for this display type and add them efficiently
                var cachedAlternates = ContentFieldSetAlternatesFactory.GetAlternates(typeFieldSetDefinition, shapeType, displayType);
                ctx.Shape.Metadata.Alternates.AddRange(cachedAlternates);
            });
        }

        return result;
    }

    async Task<IDisplayResult> IContentFieldSetDisplayDriver.BuildDisplayAsync(ContentFieldSet contentFieldSet, ContentTypeFieldSetDefinition typeFieldSetDefinition, BuildDisplayContext context)
    {
        var part = contentFieldSet as TFieldSet;

        if (part == null)
        {
            return null;
        }

        using (BuildPrefix(typeFieldSetDefinition, context.HtmlFieldPrefix))
        {
            _typeFieldSetDefinition = typeFieldSetDefinition;

            var buildDisplayContext = new BuildFieldSetDisplayContext(typeFieldSetDefinition, context);

            var result = await DisplayAsync(part, buildDisplayContext);

            _typeFieldSetDefinition = null;

            return result;
        }
    }

    public virtual Task<IDisplayResult> DisplayAsync(TFieldSet part, BuildFieldSetDisplayContext context)
    {
        return Task.FromResult(Display(part, context));
    }

    public virtual IDisplayResult Display(TFieldSet part, BuildFieldSetDisplayContext context)
    {
        return null;
    }

    protected string GetDisplayShapeType(string shapeType, BuildFieldSetDisplayContext context)
    {
        var displayMode = context.TypeFieldSetDefinition.DisplayMode();
        return !string.IsNullOrEmpty(displayMode)
            ? shapeType + DisplaySeparator + displayMode
            : shapeType;
    }

    protected string GetDisplayShapeType(BuildFieldSetDisplayContext context)
    {
        return GetDisplayShapeType(ContentFieldSetName.Of<TFieldSet>(), context);
    }

    private TempPrefix BuildPrefix(ContentTypeFieldSetDefinition typeFieldSetDefinition, string htmlFieldPrefix)
    {
        var tempPrefix = new TempPrefix(this, Prefix);

        Prefix = typeFieldSetDefinition.Name;

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
        private readonly ContentFieldSetDisplayDriver<TFieldSet> _driver;
        private readonly string _originalPrefix;

        public TempPrefix(ContentFieldSetDisplayDriver<TFieldSet> driver, string originalPrefix)
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
