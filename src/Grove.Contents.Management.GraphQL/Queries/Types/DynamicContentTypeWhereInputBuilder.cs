using GraphQL.Types;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Grove.Contents.Management.GraphQL.Options;
using Grove.Contents.Management.Metadata.Models;

namespace Grove.Contents.Management.GraphQL.Queries.Types;

public sealed class DynamicContentTypeWhereInputBuilder : DynamicContentTypeBuilder
{
    public DynamicContentTypeWhereInputBuilder(
        IHttpContextAccessor httpContextAccessor,
        IOptions<GraphQLContentOptions> contentOptionsAccessor,
        IStringLocalizer<DynamicContentTypeWhereInputBuilder> stringLocalizer)
        : base(httpContextAccessor, contentOptionsAccessor, stringLocalizer) { }

    public override void Build(ISchema schema, FieldType contentQuery, ContentTypeDefinition contentTypeDefinition, ContentItemType contentItemType)
    {
        var whereInputType = (ContentItemWhereInput)contentQuery.Arguments?.FirstOrDefault(x => x.Name == "where")?.ResolvedType;

        if (whereInputType != null)
        {
            BuildInternal(schema, contentTypeDefinition, whereInputType);
        }
    }
}
