using Grove.Contents.Management.Display.Models;
using Grove.Contents.Management.Metadata.Models;
using Grove.Display.Management;
using Grove.Display.Management.Handlers;
using Grove.Display.Management.Views;

namespace Grove.Contents.Management.Display.ContentDisplay;

public abstract class ContentFieldDisplayDriver<TField> : DisplayDriverBase, IContentFieldDisplayDriver where TField : ContentField, new()
{
    private const string DisplaySeparator = "_Display__";

    private ContentTypePartDefinition _typePartDefinition;
    private ContentPartFieldDefinition _partFieldDefinition;

    public override ShapeResult Factory<TBuilderState, TInitState>(string shapeType, Func<IBuildShapeContext, TBuilderState, ValueTask<IShape>> shapeBuilder, TBuilderState shapeBuilderState, Func<IShape, TInitState, ValueTask> initializingAsync, TInitState initializingState)
    {
        // e.g., HtmlBodyPart.Summary, HtmlBodyPart-BlogPost, BagPart-LandingPage-Services
        // context.Shape is the ContentItem shape, we need to alter the part shape

        var result = base.Factory(shapeType, shapeBuilder, shapeBuilderState, initializingAsync, initializingState);

        if (_typePartDefinition != null && _partFieldDefinition != null)
        {
            var partName = _typePartDefinition.Name;
            var fieldType = _partFieldDefinition.FieldDefinition.Name;
            var fieldName = _partFieldDefinition.Name;
            var displayMode = _partFieldDefinition.DisplayMode();
            // If the shape type and the field type only differ by the display mode
            if (!string.IsNullOrEmpty(displayMode) && shapeType == fieldType + DisplaySeparator + displayMode)
            {
                // Preserve the shape name regardless its differentiator
                result.Name($"{partName}-{fieldName}");
            }

            if (fieldType == shapeType)
            {
                // HtmlBodyPart-Description, Services-Description
                result.Differentiator($"{partName}-{fieldName}");
            }
            else
            {
                // HtmlBodyPart-Description-TextField, Services-Description-TextField
                result.Differentiator($"{partName}-{fieldName}-{shapeType}");
            }

            // The definitions will be cleared before the displaying event is raised, so
            // we need to cache it here for use in the displaying event to add alternates.
            var typePartDefinition = _typePartDefinition;
            var partFieldDefinition = _partFieldDefinition;
            result.Displaying(ctx =>
            {
                var displayType = ctx.Shape.Metadata.DisplayType;

                // Get cached alternates for this display type and add them efficiently
                var cachedAlternates = ContentFieldAlternatesFactory.GetAlternates(typePartDefinition, partFieldDefinition, shapeType, displayType);
                ctx.Shape.Metadata.Alternates.AddRange(cachedAlternates);
            });
        }

        return result;
    }

    Task<IDisplayResult> IContentFieldDisplayDriver.BuildDisplayAsync(ContentPart contentPart, ContentPartFieldDefinition partFieldDefinition, ContentTypePartDefinition typePartDefinition, BuildDisplayContext context)
    {
        if (!string.Equals(typeof(TField).Name, partFieldDefinition.FieldDefinition.Name, StringComparison.Ordinal) &&
           !string.Equals(nameof(ContentField), partFieldDefinition.FieldDefinition.Name, StringComparison.Ordinal))
        {
            return Task.FromResult(default(IDisplayResult));
        }

        var field = contentPart.Get<TField>(partFieldDefinition.Name);
        if (field != null)
        {
            BuildPrefix(typePartDefinition, partFieldDefinition, context.HtmlFieldPrefix);

            var fieldDisplayContext = new BuildFieldDisplayContext(contentPart, typePartDefinition, partFieldDefinition, context);

            _typePartDefinition = typePartDefinition;
            _partFieldDefinition = partFieldDefinition;

            var result = DisplayAsync(field, fieldDisplayContext);

            _typePartDefinition = null;
            _partFieldDefinition = null;

            return result;
        }

        return Task.FromResult(default(IDisplayResult));
    }

    public virtual Task<IDisplayResult> DisplayAsync(TField field, BuildFieldDisplayContext fieldDisplayContext)
    {
        return Task.FromResult(Display(field, fieldDisplayContext));
    }

    public virtual IDisplayResult Display(TField field, BuildFieldDisplayContext fieldDisplayContext)
    {
        return null;
    }

    protected string GetDisplayShapeType(string shapeType, BuildFieldDisplayContext context)
    {
        var displayMode = context.PartFieldDefinition.DisplayMode();
        return !string.IsNullOrEmpty(displayMode)
            ? shapeType + DisplaySeparator + displayMode
            : shapeType;
    }

    protected string GetDisplayShapeType(BuildFieldDisplayContext context)
    {
        return GetDisplayShapeType(typeof(TField).Name, context);
    }

    private void BuildPrefix(ContentTypePartDefinition typePartDefinition, ContentPartFieldDefinition partFieldDefinition, string htmlFieldPrefix)
    {
        Prefix = typePartDefinition.Name + "." + partFieldDefinition.Name;

        if (!string.IsNullOrEmpty(htmlFieldPrefix))
        {
            Prefix = htmlFieldPrefix + "." + Prefix;
        }
    }
}
