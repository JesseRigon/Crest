using Grove.Contents.Management.Display.Models;
using Grove.Contents.Management.Metadata.Models;
using Grove.Display.Management;
using Grove.Display.Management.Handlers;
using Grove.Display.Management.Views;

namespace Grove.Contents.Management.Display.ContentDisplay;

public abstract class ContentFieldDisplayDriver<TField> : DisplayDriverBase, IContentFieldDisplayDriver where TField : ContentField, new()
{
    private const string DisplaySeparator = "_Display__";

    private ContentTypeFieldSetDefinition _typeFieldSetDefinition;
    private ContentFieldDefinition _fieldDefinition;

    public override ShapeResult Factory<TBuilderState, TInitState>(string shapeType, Func<IBuildShapeContext, TBuilderState, ValueTask<IShape>> shapeBuilder, TBuilderState shapeBuilderState, Func<IShape, TInitState, ValueTask> initializingAsync, TInitState initializingState)
    {
        // e.g., HtmlBodyFieldSet.Summary, HtmlBodyFieldSet-BlogPost, Taxonomy-LandingPage-Categories
        // context.Shape is the ContentItem shape, we need to alter the part shape

        var result = base.Factory(shapeType, shapeBuilder, shapeBuilderState, initializingAsync, initializingState);

        if (_typeFieldSetDefinition != null && _fieldDefinition != null)
        {
            var partName = _typeFieldSetDefinition.Name;
            var fieldType = _fieldDefinition.FieldTypeDefinition.Name;
            var fieldName = _fieldDefinition.Name;
            var displayMode = _fieldDefinition.DisplayMode();
            // If the shape type and the field type only differ by the display mode
            if (!string.IsNullOrEmpty(displayMode) && shapeType == fieldType + DisplaySeparator + displayMode)
            {
                // Preserve the shape name regardless its differentiator
                result.Name($"{partName}-{fieldName}");
            }

            if (fieldType == shapeType)
            {
                // HtmlBodyFieldSet-Description, Services-Description
                result.Differentiator($"{partName}-{fieldName}");
            }
            else
            {
                // HtmlBodyFieldSet-Description-TextField, Services-Description-TextField
                result.Differentiator($"{partName}-{fieldName}-{shapeType}");
            }

            // The definitions will be cleared before the displaying event is raised, so
            // we need to cache it here for use in the displaying event to add alternates.
            var typeFieldSetDefinition = _typeFieldSetDefinition;
            var partFieldDefinition = _fieldDefinition;
            result.Displaying(ctx =>
            {
                var displayType = ctx.Shape.Metadata.DisplayType;

                // Get cached alternates for this display type and add them efficiently
                var cachedAlternates = ContentFieldAlternatesFactory.GetAlternates(typeFieldSetDefinition, partFieldDefinition, shapeType, displayType);
                ctx.Shape.Metadata.Alternates.AddRange(cachedAlternates);
            });
        }

        return result;
    }

    Task<IDisplayResult> IContentFieldDisplayDriver.BuildDisplayAsync(ContentFieldSet contentFieldSet, ContentFieldDefinition partFieldDefinition, ContentTypeFieldSetDefinition typeFieldSetDefinition, BuildDisplayContext context)
    {
        if (!string.Equals(typeof(TField).Name, partFieldDefinition.FieldTypeDefinition.Name, StringComparison.Ordinal) &&
           !string.Equals(nameof(ContentField), partFieldDefinition.FieldTypeDefinition.Name, StringComparison.Ordinal))
        {
            return Task.FromResult(default(IDisplayResult));
        }

        var field = contentFieldSet.Get<TField>(partFieldDefinition.Name);
        if (field != null)
        {
            BuildPrefix(typeFieldSetDefinition, partFieldDefinition, context.HtmlFieldPrefix);

            var fieldDisplayContext = new BuildFieldDisplayContext(contentFieldSet, typeFieldSetDefinition, partFieldDefinition, context);

            _typeFieldSetDefinition = typeFieldSetDefinition;
            _fieldDefinition = partFieldDefinition;

            var result = DisplayAsync(field, fieldDisplayContext);

            _typeFieldSetDefinition = null;
            _fieldDefinition = null;

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
        var displayMode = context.FieldDefinition.DisplayMode();
        return !string.IsNullOrEmpty(displayMode)
            ? shapeType + DisplaySeparator + displayMode
            : shapeType;
    }

    protected string GetDisplayShapeType(BuildFieldDisplayContext context)
    {
        return GetDisplayShapeType(typeof(TField).Name, context);
    }

    private void BuildPrefix(ContentTypeFieldSetDefinition typeFieldSetDefinition, ContentFieldDefinition partFieldDefinition, string htmlFieldPrefix)
    {
        Prefix = typeFieldSetDefinition.Name + "." + partFieldDefinition.Name;

        if (!string.IsNullOrEmpty(htmlFieldPrefix))
        {
            Prefix = htmlFieldPrefix + "." + Prefix;
        }
    }
}
