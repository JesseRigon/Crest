using GraphQL.Types;
using Crest.ContentManagement.Metadata.Models;

namespace Crest.ContentManagement.GraphQL.Queries.Types;

public interface IContentFieldProvider
{
    FieldType GetField(ISchema schema, ContentPartFieldDefinition field, string namedPartTechnicalName, string customFieldName = null);

    bool HasField(ISchema schema, ContentPartFieldDefinition field);

    FieldTypeIndexDescriptor GetFieldIndex(ContentPartFieldDefinition field);

    bool HasFieldIndex(ContentPartFieldDefinition field);
}
