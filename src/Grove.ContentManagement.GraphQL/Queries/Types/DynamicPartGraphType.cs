using GraphQL.Types;
using Grove.ContentManagement.Metadata.Models;

namespace Grove.ContentManagement.GraphQL.Queries.Types;

public sealed class DynamicPartGraphType : ObjectGraphType<ContentPart>
{
    public DynamicPartGraphType(
        ContentTypePartDefinition part,
        ISchema schema,
        IEnumerable<IContentFieldProvider> contentFieldProviders)
    {
        Name = part.Name;

        foreach (var field in part.PartDefinition.Fields)
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
