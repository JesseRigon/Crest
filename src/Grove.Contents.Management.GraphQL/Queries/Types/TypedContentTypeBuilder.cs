using GraphQL;
using GraphQL.Resolvers;
using GraphQL.Types;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.Data.Protocols.GraphQL;
using Grove.Contents.Management.GraphQL.Options;
using Grove.Contents.Management.Metadata.Models;

namespace Grove.Contents.Management.GraphQL.Queries.Types;

public sealed class TypedContentTypeBuilder : IContentTypeBuilder
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly GraphQLContentOptions _contentOptions;

    public TypedContentTypeBuilder(
        IHttpContextAccessor httpContextAccessor,
        IOptions<GraphQLContentOptions> contentOptionsAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
        _contentOptions = contentOptionsAccessor.Value;
    }

    public void Build(ISchema schema, FieldType contentQuery, ContentTypeDefinition contentTypeDefinition, ContentItemType contentItemType)
    {
        var serviceProvider = _httpContextAccessor.HttpContext.RequestServices;
        var typeActivator = serviceProvider.GetService<ITypeActivatorFactory<ContentFieldSet>>();

        if (_contentOptions.ShouldHide(contentTypeDefinition))
        {
            return;
        }

        foreach (var part in contentTypeDefinition.FieldSets)
        {
            if (_contentOptions.ShouldSkip(part))
            {
                continue;
            }

            var partName = part.Name;
            var partFieldName = partName.ToFieldName();

            // Check if another builder has already added a field for this part.
            if (contentItemType.HasField(partFieldName))
            {
                continue;
            }

            var queryGraphType = schema.AdditionalTypeInstances
                .FirstOrDefault(x => x is IObjectGraphType && ContentFieldSetName.Of(x.GetType().BaseType.GetGenericArguments().First()) == part.FieldSetDefinition.Name) as IObjectGraphType;

            var collapseFieldSet = _contentOptions.ShouldCollapse(part);

            if (queryGraphType != null)
            {
                if (collapseFieldSet)
                {
                    foreach (var field in queryGraphType.Fields)
                    {
                        if (_contentOptions.ShouldSkip(queryGraphType.GetType(), field.Name) ||
                            contentItemType.HasFieldIgnoreCase(field.Name))
                        {
                            continue;
                        }

                        var partActivator = typeActivator.GetTypeActivator(part.FieldSetDefinition.Name);
                        var partType = partActivator.Type;
                        var rolledUpField = new FieldType
                        {
                            Name = field.Name,
                            Type = field.Type,
                            Description = field.Description,
                            DeprecationReason = field.DeprecationReason,
                            Arguments = field.Arguments,
                            Resolver = new FuncFieldResolver<ContentItem, object>(context =>
                            {
                                var nameToResolve = partName;
                                var resolvedFieldSet = context.Source.Get(partType, nameToResolve);

                                return field.Resolver.ResolveAsync(new ResolveFieldContext
                                {
                                    Arguments = context.Arguments,
                                    Source = resolvedFieldSet,
                                    FieldDefinition = field,
                                    UserContext = context.UserContext,
                                    RequestServices = context.RequestServices,
                                });
                            }),
                        };

                        contentItemType.AddField(rolledUpField);
                    }
                }
                else
                {
                    contentItemType
                        .Field(partFieldName, queryGraphType.GetType())
                        .Description(queryGraphType.Description)
                        .Resolve(context =>
                        {
                            var nameToResolve = partName;
                            var typeToResolve = context.FieldDefinition.ResolvedType.GetType().BaseType.GetGenericArguments().First();

                            return context.Source.Get(typeToResolve, nameToResolve);
                        });
                }
            }

            var inputGraphTypeResolved = schema.AdditionalTypeInstances
                .FirstOrDefault(x => x is IInputObjectGraphType && x.GetType().BaseType.GetGenericArguments().FirstOrDefault()?.Name == part.FieldSetDefinition.Name) as IInputObjectGraphType;

            if (inputGraphTypeResolved != null)
            {
                var whereArgument = contentQuery.Arguments.FirstOrDefault(x => x.Name == "where");
                if (whereArgument == null)
                {
                    return;
                }

                var whereInput = (ContentItemWhereInput)whereArgument.ResolvedType;

                if (collapseFieldSet)
                {
                    foreach (var field in inputGraphTypeResolved.Fields)
                    {
                        whereInput.AddField(field.WithFieldSetCollapsedMetaData().WithFieldSetNameMetaData(partName));
                    }
                }
                else
                {
                    whereInput.AddField(new FieldType
                    {
                        Type = inputGraphTypeResolved.GetType(),
                        Name = partFieldName,
                        Description = inputGraphTypeResolved.Description,
                    }.WithFieldSetNameMetaData(partName));
                }
            }
        }
    }

    public void Clear()
    {
    }
}
