using GraphQL.Resolvers;
using GraphQL.Types;
using Microsoft.Extensions.Localization;
using Grove.Contents.Management;
using Grove.Contents.Management.GraphQL.Queries.Types;
using Grove.Contents.Management.Metadata.Models;
using Grove.Data.Types.Spatial.Fields;

namespace Grove.Data.Types.Spatial.GraphQL;

public class GeoPointFieldProvider : IContentFieldProvider
{
    protected readonly IStringLocalizer S;
    public GeoPointFieldProvider(IStringLocalizer<GeoPointFieldProvider> stringLocalizer)
    {
        S = stringLocalizer;
    }

    private static object HandleFieldResolving(ContentElement field)
        => new { Latitude = ((GeoPointField)field).Latitude, Longitude = ((GeoPointField)field).Longitude };

    public FieldType GetField(ISchema schema, ContentFieldDefinition field, string namedFieldSetTechnicalName, string customFieldName = null)
    {
        if (field.FieldTypeDefinition.Name != nameof(GeoPointField))
        {
            return null;
        }

        return new FieldType
        {
            Name = customFieldName ?? schema.NameConverter.NameForField(field.Name, null),
            Description = S["Geo point field"],
            Type = typeof(GeoPointGraphType),
            ResolvedType = new GeoPointGraphType(),
            Resolver = new FuncFieldResolver<ContentElement, object>(context =>
            {
                // Check if part has been collapsed by trying to get the parent part.
                ContentElement contentFieldSet = context.Source.Get<ContentFieldSet>(field.FieldSetDefinition.Name);

                // Part is not collapsed, access field directly.
                contentFieldSet ??= context.Source;

                var contentField = contentFieldSet?.Get<GeoPointField>(field.Name);

                contentField ??= context.Source.Get<GeoPointField>(field.Name);

                return contentField == null ? null : HandleFieldResolving(contentField);
            }),
        };
    }

    public FieldTypeIndexDescriptor GetFieldIndex(ContentFieldDefinition field)
    {
        return null;
    }

    public bool HasField(ISchema schema, ContentFieldDefinition field)
    {
        if (field.FieldTypeDefinition.Name == nameof(GeoPointField))
        {
            return true;
        }
        return false;
    }

    public bool HasFieldIndex(ContentFieldDefinition field)
        => false;
}
