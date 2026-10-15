using GraphQL.Types;
using Grove.Contents.Management.Metadata.Models;

namespace Grove.Contents.Management.GraphQL.Queries.Types;

public interface IContentFieldProvider
{
    FieldType GetField(ISchema schema, ContentPartFieldDefinition field, string namedPartTechnicalName, string customFieldName = null);

    bool HasField(ISchema schema, ContentPartFieldDefinition field);

    FieldTypeIndexDescriptor GetFieldIndex(ContentPartFieldDefinition field);

    bool HasFieldIndex(ContentPartFieldDefinition field);
}
