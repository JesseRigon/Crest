using GraphQL.Types;
using Microsoft.Extensions.Localization;
using Grove.Data.Protocols.GraphQL.Queries;
using Grove.Contents.Management.Metadata.Models;

namespace Grove.Contents.Management.GraphQL.Queries.Types;

public sealed class DynamicFieldSetWhereInputGraphType : WhereInputObjectGraphType<ContentFieldSet>
{
    public DynamicFieldSetWhereInputGraphType(
        ContentTypeFieldSetDefinition part,
        ISchema schema,
        IEnumerable<IContentFieldProvider> contentFieldProviders,
        IStringLocalizer<DynamicFieldSetWhereInputGraphType> stringLocalizer)
        : base(stringLocalizer)
    {
        Name = $"{part.Name}WhereInput";

        foreach (var field in part.FieldSetDefinition.Fields)
        {
            foreach (var fieldProvider in contentFieldProviders)
            {
                var fieldType = fieldProvider.GetField(schema, field, part.Name);

                if (fieldType != null)
                {
                    AddScalarFilterFields(fieldType.Type, fieldType.Name, fieldType.Description, fieldProvider.GetFieldIndex(field)?.AliasName, part.Name, field.Name);
                    break;
                }
            }
        }
    }
}
