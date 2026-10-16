using System.Collections.Frozen;
using GraphQL;
using GraphQL.Resolvers;
using GraphQL.Types;
using Microsoft.Extensions.Localization;
using Grove.Data.Protocols.GraphQL.Queries.Types;
using Grove.Contents.Fields.Fields;
using Grove.Contents.Fields.Indexing.SQL;
using Grove.Contents.Management;
using Grove.Contents.Management.GraphQL.Queries.Types;
using Grove.Contents.Management.Metadata.Models;

namespace Grove.Contents.Fields.GraphQL.Fields;

public class ContentFieldsProvider : IContentFieldProvider
{
    private static readonly FrozenDictionary<string, FieldTypeDescriptor> s_contentFieldTypeMappings = new Dictionary<string, FieldTypeDescriptor>()
    {
        {
            nameof(BooleanField),
            new FieldTypeDescriptor
            {
                Description = S => S["Boolean field"],
                FieldType = typeof(BooleanGraphType),
                ResolvedType = new BooleanGraphType(),
                UnderlyingType = typeof(BooleanField),
                FieldAccessor = field => ((BooleanField)field).Value,
                IndexType = typeof(BooleanFieldIndex),
                IndexProperty = nameof(BooleanFieldIndex.Boolean),
            }
        },
        {
            nameof(DateField),
            new FieldTypeDescriptor
            {
                Description = S => S["Date field"],
                FieldType = typeof(DateGraphType),
                ResolvedType = new DateGraphType(),
                UnderlyingType = typeof(DateField),
                FieldAccessor = field => ((DateField)field).Value,
                IndexType = typeof(DateFieldIndex),
                IndexProperty = nameof(DateFieldIndex.Date),
            }
        },
        {
            nameof(DateTimeField),
            new FieldTypeDescriptor
            {
                Description = S => S["Date & time field"],
                FieldType = typeof(DateTimeGraphType),
                ResolvedType = new DateTimeGraphType(),
                UnderlyingType = typeof(DateTimeField),
                FieldAccessor = field => ((DateTimeField)field).Value,
                IndexType = typeof(DateTimeFieldIndex),
                IndexProperty = nameof(DateTimeFieldIndex.DateTime),
            }
        },
        {
            nameof(NumericField),
            new FieldTypeDescriptor
            {
                Description = S => S["Numeric field"],
                FieldType = typeof(DecimalGraphType),
                ResolvedType = new DecimalGraphType(),
                UnderlyingType = typeof(NumericField),
                FieldAccessor = field => ((NumericField)field).Value,
                IndexType = typeof(NumericFieldIndex),
                IndexProperty = nameof(NumericFieldIndex.Numeric),
            }
        },
        {
            nameof(TextField),
            new FieldTypeDescriptor
            {
                Description = S => S["Text field"],
                FieldType = typeof(StringGraphType),
                ResolvedType = new StringGraphType(),
                UnderlyingType = typeof(TextField),
                FieldAccessor = field => ((TextField)field).Text,
                IndexType = typeof(TextFieldIndex),
                IndexProperty = nameof(TextFieldIndex.Text),
            }
        },
        {
            nameof(TimeField),
            new FieldTypeDescriptor
            {
                Description = S => S["Time field"],
                FieldType = typeof(TimeSpanGraphType),
                ResolvedType = new TimeSpanGraphType(),
                UnderlyingType = typeof(TimeField),
                FieldAccessor = field => ((TimeField)field).Value,
                IndexType = typeof(TimeFieldIndex),
                IndexProperty = nameof(TimeFieldIndex.Time),
            }
        },
        {
            nameof(MultiTextField),
            new FieldTypeDescriptor
            {
                Description = S => S["Multi text field"],
                FieldType = typeof(ListGraphType<StringGraphType>),
                ResolvedType = new ListGraphType(new StringGraphType()),
                UnderlyingType = typeof(MultiTextField),
                FieldAccessor = field => ((MultiTextField)field).Values,
            }
        },
    }.ToFrozenDictionary();

    protected readonly IStringLocalizer S;

    public ContentFieldsProvider(IStringLocalizer<ContentFieldsProvider> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public FieldType GetField(ISchema schema, ContentFieldDefinition field, string namedFieldSetTechnicalName, string customFieldName)
    {
        if (!s_contentFieldTypeMappings.TryGetValue(field.FieldTypeDefinition.Name, out var fieldDescriptor))
        {
            return null;
        }

        return new FieldType
        {
            Name = customFieldName ?? schema.NameConverter.NameForField(field.Name, null),
            Description = fieldDescriptor.Description(S),
            Type = fieldDescriptor.FieldType,
            ResolvedType = fieldDescriptor.ResolvedType,
            Resolver = new FuncFieldResolver<ContentElement, object>(context =>
            {
                // Check if part has been collapsed by trying to get the parent part.
                ContentElement contentFieldSet = context.Source.Get<ContentFieldSet>(field.FieldSetDefinition.Name);

                // Part is not collapsed, access field directly.
                contentFieldSet ??= context.Source;

                var contentField = contentFieldSet?.Get(fieldDescriptor.UnderlyingType, field.Name);

                contentField ??= context.Source.Get(fieldDescriptor.UnderlyingType, field.Name);

                return contentField == null ? null : fieldDescriptor.FieldAccessor(contentField);
            }),
        };
    }

    public bool HasField(ISchema schema, ContentFieldDefinition field)
        => s_contentFieldTypeMappings.ContainsKey(field.FieldTypeDefinition.Name);

    public FieldTypeIndexDescriptor GetFieldIndex(ContentFieldDefinition field)
    {
        if (!HasFieldIndex(field))
        {
            return null;
        }

        var fieldDescriptor = s_contentFieldTypeMappings[field.FieldTypeDefinition.Name];

        return new FieldTypeIndexDescriptor
        {
            AliasName = $"{field.FieldSetDefinition.Name.ToFieldName()}:{field.Name.ToCamelCase()}:{fieldDescriptor.IndexProperty}",
            Index = fieldDescriptor.IndexType.Name,
            IndexType = fieldDescriptor.IndexType,
        };
    }

    public bool HasFieldIndex(ContentFieldDefinition field)
        => s_contentFieldTypeMappings.TryGetValue(field.FieldTypeDefinition.Name, out var fieldTypeDescriptor) &&
        fieldTypeDescriptor.IndexType != null &&
        !string.IsNullOrWhiteSpace(fieldTypeDescriptor.IndexProperty);

    private sealed class FieldTypeDescriptor
    {
        public Func<IStringLocalizer, string> Description { get; set; }

        public Type FieldType { get; set; }

        public Type UnderlyingType { get; set; }

        public required IGraphType ResolvedType { get; set; }

        public Func<ContentElement, object> FieldAccessor { get; set; }

        public string IndexProperty { get; set; }

        public Type IndexType { get; set; }
    }
}
