using GraphQL.Types;
using Grove.Contents.Management.Metadata.Models;

namespace Grove.Contents.Management.GraphQL.Queries.Types;

public sealed class DynamicFieldSetGraphType : ObjectGraphType<ContentFieldSet>
{
    public DynamicFieldSetGraphType(
        ContentTypeFieldSetDefinition part,
        ISchema schema,
        IEnumerable<IContentFieldProvider> contentFieldProviders)
    {
        Name = part.Name;

        foreach (var field in part.FieldSetDefinition.Fields)
        {
            foreach (var fieldProvider in contentFieldProviders)
            {
                var fieldType = fieldProvider.GetField(schema, field, part.Name);
                if (fieldType != null)
                {
                    AddField(fieldType);
                    break;
                }
            }
        }
    }
}
