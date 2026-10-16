using GraphQL.Types;
using Grove.Contents.Management.Metadata.Models;

namespace Grove.Contents.Management.GraphQL.Queries.Types;

public interface IContentFieldProvider
{
    FieldType GetField(ISchema schema, ContentFieldDefinition field, string namedFieldSetTechnicalName, string customFieldName = null);

    bool HasField(ISchema schema, ContentFieldDefinition field);

    FieldTypeIndexDescriptor GetFieldIndex(ContentFieldDefinition field);

    bool HasFieldIndex(ContentFieldDefinition field);
}
