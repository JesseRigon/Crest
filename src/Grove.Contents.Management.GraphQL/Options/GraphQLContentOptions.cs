using GraphQL;
using GraphQL.Types;
using Grove.Contents.Management.GraphQL.Settings;
using Grove.Contents.Management.Metadata.Models;

namespace Grove.Contents.Management.GraphQL.Options;

public class GraphQLContentOptions
{
    public IEnumerable<GraphQLContentTypeOption> ContentTypeOptions { get; set; } = [];

    public IEnumerable<GraphQLContentFieldSetOption> FieldSetOptions { get; set; } = [];

    public IEnumerable<GraphQLField> HiddenFields { get; set; } = [];

    /// <summary>
    /// By default, only content types without stereotypes are included.
    /// This option enables you to explicitly add additional stereotypes for discovery.
    /// </summary>
    public HashSet<string> DiscoverableSterotypes { get; } = new(StringComparer.OrdinalIgnoreCase);

    public GraphQLContentOptions ConfigureContentType(string contentType, Action<GraphQLContentTypeOption> action)
    {
        var option = new GraphQLContentTypeOption(contentType);

        action(option);

        ContentTypeOptions = ContentTypeOptions.Union([option]);

        return this;
    }

    public GraphQLContentOptions ConfigureFieldSet<TContentFieldSet>(Action<GraphQLContentFieldSetOption> action)
        where TContentFieldSet : ContentFieldSet
    {
        var option = new GraphQLContentFieldSetOption<TContentFieldSet>();

        action(option);

        FieldSetOptions = FieldSetOptions.Union([option]);

        return this;
    }

    public GraphQLContentOptions ConfigureFieldSet(string fieldSetName, Action<GraphQLContentFieldSetOption> action)
    {
        var option = new GraphQLContentFieldSetOption(fieldSetName);

        action(option);

        FieldSetOptions = FieldSetOptions.Union([option]);

        return this;
    }

    public GraphQLContentOptions IgnoreField<TGraphType>(string fieldName) where TGraphType : IObjectGraphType
    {
        HiddenFields = HiddenFields.Union([new GraphQLField<TGraphType>(fieldName)]);

        return this;
    }

    public GraphQLContentOptions IgnoreField(Type fieldType, string fieldName)
    {
        HiddenFields = HiddenFields.Union([new GraphQLField(fieldType, fieldName)]);

        return this;
    }

    /// <summary>
    /// Collapsing works at a hierarchy
    ///
    /// If the Content Type is marked at collapsed, then all parts are collapsed.
    /// If the Content Type is not marked collapsed, then it falls down to the content type under it.
    /// If the Field set at a top level is marked collapsed, then it will trump above.
    /// </summary>
    /// <param name="definition"></param>
    /// <returns></returns>
    internal bool ShouldCollapse(ContentTypeFieldSetDefinition definition)
    {
        if (IsCollapsedByDefault(definition))
        {
            return true;
        }

        var settings = definition.GetSettings<GraphQLContentTypeFieldSetSettings>();

        if (settings.Collapse)
        {
            return true;
        }

        return false;
    }

    public bool IsCollapsedByDefault(ContentTypeFieldSetDefinition definition)
    {
        var contentType = definition.ContentTypeDefinition.Name;
        var partName = definition.FieldSetDefinition.Name;

        if (contentType == partName)
        {
            return true;
        }

        var contentTypeOption = ContentTypeOptions.FirstOrDefault(ctp => ctp.ContentType == contentType);

        if (contentTypeOption != null)
        {
            if (contentTypeOption.Collapse)
            {
                return true;
            }

            var contentTypeFieldSetOption = contentTypeOption.FieldSetOptions.FirstOrDefault(p => p.Name == partName);

            if (contentTypeFieldSetOption != null)
            {
                if (contentTypeFieldSetOption.Collapse)
                {
                    return true;
                }
            }
        }

        var contentFieldSetOption = FieldSetOptions.FirstOrDefault(p => p.Name == partName);

        if (contentFieldSetOption != null)
        {
            if (contentFieldSetOption.Collapse)
            {
                return true;
            }
        }

        return false;
    }

    internal bool ShouldSkip(ContentTypeFieldSetDefinition definition)
    {
        if (IsHiddenByDefault(definition))
        {
            return true;
        }

        var settings = definition.GetSettings<GraphQLContentTypeFieldSetSettings>();

        if (settings.Hidden)
        {
            return true;
        }

        return false;
    }

    public bool IsHiddenByDefault(string contentType)
    {
        ArgumentException.ThrowIfNullOrEmpty(contentType);

        var contentTypeOption = ContentTypeOptions.FirstOrDefault(ctp => ctp.ContentType.Equals(contentType, StringComparison.OrdinalIgnoreCase));

        return contentTypeOption?.Hidden ?? false;
    }

    public bool ShouldHide(ContentTypeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var settings = definition.GetSettings<GraphQLContentTypeSettings>();

        if (settings.Hidden)
        {
            return true;
        }

        return IsHiddenByDefault(definition.Name);
    }

    internal bool ShouldSkip(Type fieldType, string fieldName)
    {
        return HiddenFields
            .Any(x => x.FieldType == fieldType && x.FieldName.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
    }

    public bool IsHiddenByDefault(ContentTypeFieldSetDefinition definition)
    {
        var contentType = definition.ContentTypeDefinition.Name;
        var partName = definition.FieldSetDefinition.Name;

        var contentTypeOption = ContentTypeOptions.FirstOrDefault(ctp => ctp.ContentType == contentType);

        if (contentTypeOption != null)
        {
            if (contentTypeOption.Hidden)
            {
                return true;
            }

            var contentTypeFieldSetOption = contentTypeOption.FieldSetOptions.FirstOrDefault(p => p.Name == partName);

            if (contentTypeFieldSetOption != null)
            {
                if (contentTypeFieldSetOption.Hidden)
                {
                    return true;
                }
            }
        }

        var contentFieldSetOption = FieldSetOptions.FirstOrDefault(p => p.Name == partName);

        if (contentFieldSetOption != null)
        {
            if (contentFieldSetOption.Hidden)
            {
                return true;
            }
        }

        return false;
    }

    internal static string GetFieldName(ContentTypeFieldSetDefinition definition, string fieldSetName, string fieldName)
    {
        var settings = definition.GetSettings<GraphQLContentTypeFieldSetSettings>();

        if (settings.PreventFieldNameCollision)
        {
            return fieldSetName.ToFieldName() + fieldName.ToPascalCase();
        }

        return fieldName.ToCamelCase();
    }
}
