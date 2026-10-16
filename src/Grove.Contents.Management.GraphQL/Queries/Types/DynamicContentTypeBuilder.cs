using GraphQL.Resolvers;
using GraphQL.Types;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Grove.Data.Protocols.GraphQL;
using Grove.Data.Protocols.GraphQL.Queries;
using Grove.Contents.Management.GraphQL.Options;
using Grove.Contents.Management.Metadata.Models;

namespace Grove.Contents.Management.GraphQL.Queries.Types;

public abstract class DynamicContentTypeBuilder : IContentTypeBuilder
{
    protected readonly IHttpContextAccessor _httpContextAccessor;
    protected readonly GraphQLContentOptions _contentOptions;

    protected readonly IStringLocalizer S;

    private readonly Dictionary<string, FieldType> _dynamicFieldSetFields;

    protected DynamicContentTypeBuilder(
        IHttpContextAccessor httpContextAccessor,
        IOptions<GraphQLContentOptions> contentOptionsAccessor,
        IStringLocalizer<DynamicContentTypeBuilder> localizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _contentOptions = contentOptionsAccessor.Value;
        _dynamicFieldSetFields = [];

        S = localizer;
    }

    public abstract void Build(ISchema schema, FieldType contentQuery, ContentTypeDefinition contentTypeDefinition, ContentItemType contentItemType);

    protected void BuildInternal(ISchema schema, ContentTypeDefinition contentTypeDefinition, IComplexGraphType graphType)
    {
        if (_contentOptions.ShouldHide(contentTypeDefinition))
        {
            return;
        }
        var serviceProvider = _httpContextAccessor.HttpContext.RequestServices;
        var contentFieldProviders = serviceProvider.GetServices<IContentFieldProvider>().ToArray();

        foreach (var part in contentTypeDefinition.FieldSets)
        {
            var partName = part.Name;

            // This builder only handles parts with fields.
            if (!part.FieldSetDefinition.Fields.Any())
            {
                continue;
            }

            if (_contentOptions.ShouldSkip(part))
            {
                continue;
            }

            if (!part.FieldSetDefinition.Fields.Any(field => contentFieldProviders.Any(fieldProvider => fieldProvider.HasField(schema, field))))
            {
                continue;
            }

            if (_contentOptions.ShouldCollapse(part))
            {
                foreach (var field in part.FieldSetDefinition.Fields)
                {
                    foreach (var fieldProvider in contentFieldProviders)
                    {
                        var customFieldName = GraphQLContentOptions.GetFieldName(part, part.Name, field.Name);

                        var contentFieldType = fieldProvider.GetField(schema, field, part.Name, customFieldName);

                        if (contentFieldType != null)
                        {
                            if (_contentOptions.ShouldSkip(contentFieldType.Type, contentFieldType.Name) ||
                                graphType.HasFieldIgnoreCase(contentFieldType.Name))
                            {
                                continue;
                            }

                            if (graphType is IFilterInputObjectGraphType curInputGraphType)
                            {
                                var index = fieldProvider.GetFieldIndex(field);
                                if (index != null)
                                {
                                    curInputGraphType.AddScalarFilterFields(contentFieldType.Type, contentFieldType.Name, contentFieldType.Description, index.AliasName, part.Name, field.Name);
                                }
                            }
                            else if (graphType is IObjectGraphType curObjectGraphType)
                            {
                                curObjectGraphType.AddField(contentFieldType);
                            }

                            break;
                        }
                    }
                }

                continue;
            }

            // Check if another builder has already added a field for this part.
            var partFieldName = partName.ToFieldName();
            var partFieldType = graphType.GetField(partFieldName);

            if (partFieldType != null)
            {
                // Add dynamic content fields to the registered part type.
                var partContentItemType = schema.AdditionalTypeInstances
                    .Where(type => type is IObjectGraphType || type is IFilterInputObjectGraphType)
                    .Where(type => type.GetType() == partFieldType.Type)
                    .FirstOrDefault() as IComplexGraphType;

                if (partContentItemType != null)
                {
                    foreach (var field in part.FieldSetDefinition.Fields)
                    {
                        foreach (var fieldProvider in contentFieldProviders)
                        {
                            var contentFieldType = fieldProvider.GetField(schema, field, part.Name);

                            if (contentFieldType != null)
                            {
                                if (_contentOptions.ShouldSkip(contentFieldType.Type, contentFieldType.Name) ||
                                    partContentItemType.HasFieldIgnoreCase(contentFieldType.Name))
                                {
                                    continue;
                                }

                                if (partContentItemType is IFilterInputObjectGraphType partInputContentItemType)
                                {
                                    var index = fieldProvider.GetFieldIndex(field);
                                    if (index != null)
                                    {
                                        partInputContentItemType.AddScalarFilterFields(contentFieldType.Type, contentFieldType.Name, contentFieldType.Description, index.AliasName, part.Name, field.Name);
                                    }
                                }
                                else if (partContentItemType is IObjectGraphType partContentItemObjectType)
                                {
                                    partContentItemObjectType.AddField(contentFieldType);
                                }

                                break;
                            }
                        }
                    }
                }

                continue;
            }

            if (_dynamicFieldSetFields.TryGetValue(partName, out var fieldType))
            {
                if (graphType is IFilterInputObjectGraphType curInputGraphType)
                {
                    curInputGraphType.AddScalarFilterFields(fieldType.Type, fieldType.Name, fieldType.Description, null, null, null);
                }
                else if (graphType is IObjectGraphType curObjectGraphType)
                {
                    curObjectGraphType.AddField(fieldType);
                }

                continue;
            }

            if (graphType is IFilterInputObjectGraphType inputGraphType)
            {
                if (part.FieldSetDefinition.Fields.Any(field => contentFieldProviders.Any(cfp => cfp.HasFieldIndex(field))))
                {
                    var field = new FieldType
                    {
                        Name = partFieldName,
                        Description = S["Represents a {0}.", part.FieldSetDefinition.Name],
                        Type = typeof(DynamicFieldSetWhereInputGraphType),
                        ResolvedType = new DynamicFieldSetWhereInputGraphType(part, schema, contentFieldProviders, serviceProvider.GetRequiredService<IStringLocalizer<DynamicFieldSetWhereInputGraphType>>()),
                    };

                    inputGraphType.AddField(field);
                    _dynamicFieldSetFields[partName] = field;
                }
            }
            else if (graphType is IObjectGraphType objectGraphType)
            {
                var field = new FieldType
                {
                    Name = partFieldName,
                    Description = S["Represents a {0}.", part.FieldSetDefinition.Name],
                    Type = typeof(DynamicFieldSetGraphType),
                    ResolvedType = new DynamicFieldSetGraphType(part, schema, contentFieldProviders),
                    Resolver = new FuncFieldResolver<ContentElement, object>(context =>
                    {
                        var nameToResolve = partName;
                        var typeToResolve = context.FieldDefinition.ResolvedType.GetType().BaseType.GetGenericArguments().First();

                        return context.Source.Get(typeToResolve, nameToResolve);
                    }),
                };

                objectGraphType.AddField(field);
                _dynamicFieldSetFields[partName] = field;
            }
        }
    }

    public void Clear()
    {
        _dynamicFieldSetFields.Clear();
    }
}
