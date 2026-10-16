using GraphQL.Resolvers;
using GraphQL.Types;
using Microsoft.AspNetCore.Http;
using Grove.Contents.Management;
using Grove.Contents.Management.GraphQL.Queries.Types;
using Grove.Contents.Management.Metadata.Models;

namespace Grove.Contents.Fields.GraphQL.Fields;

public class ObjectGraphTypeFieldProvider : IContentFieldProvider
{
    public ObjectGraphTypeFieldProvider()
    {
    }

    public FieldType GetField(ISchema schema, ContentFieldDefinition field, string namedFieldSetTechnicalName, string customFieldName)
    {
        var queryGraphType = GetObjectGraphType(schema, field);

        if (queryGraphType != null)
        {
            return new FieldType
            {
                Name = customFieldName ?? field.Name,
                Description = field.FieldTypeDefinition.Name,
                ResolvedType = queryGraphType,
                Resolver = new FuncFieldResolver<ContentElement, ContentElement>(context =>
                {
                    var typeToResolve = context.FieldDefinition.ResolvedType.GetType().BaseType
                        .GetGenericArguments().First();

                    // Check if part has been collapsed by trying to get the parent part.
                    ContentElement contentFieldSet = context.Source.Get<ContentFieldSet>(field.FieldSetDefinition.Name);

                    // Part is not collapsed, access field directly.
                    contentFieldSet ??= context.Source;

                    var contentField = contentFieldSet?.Get(typeToResolve, field.Name);
                    return contentField;
                }),
            };
        }

        return null;
    }

    public FieldTypeIndexDescriptor GetFieldIndex(ContentFieldDefinition field)
    {
        return null;
    }

    public bool HasField(ISchema schema, ContentFieldDefinition field) => GetObjectGraphType(schema, field) != null;

    public bool HasFieldIndex(ContentFieldDefinition field) => false;

    private static IObjectGraphType GetObjectGraphType(ISchema schema, ContentFieldDefinition field) =>
        schema.AdditionalTypeInstances
            .FirstOrDefault(x => x is IObjectGraphType && x.GetType().BaseType.GetGenericArguments().First().Name == field.FieldTypeDefinition.Name) as IObjectGraphType;
}
