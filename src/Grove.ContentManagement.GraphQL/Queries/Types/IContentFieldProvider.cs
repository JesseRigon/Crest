using GraphQL.Types;
using Grove.ContentManagement.Metadata.Models;

namespace Grove.ContentManagement.GraphQL.Queries.Types;

public interface IContentFieldProvider
{
    FieldType GetField(ISchema schema, ContentPartFieldDefinition field, string namedPartTechnicalName, string customFieldName = null);

    bool HasField(ISchema schema, ContentPartFieldDefinition field);

    FieldTypeIndexDescriptor GetFieldIndex(ContentPartFieldDefinition field);

    bool HasFieldIndex(ContentPartFieldDefinition field);
}
