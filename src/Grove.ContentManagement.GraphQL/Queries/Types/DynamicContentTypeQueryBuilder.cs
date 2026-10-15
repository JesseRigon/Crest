using GraphQL.Types;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Grove.ContentManagement.GraphQL.Options;
using Grove.ContentManagement.Metadata.Models;

namespace Grove.ContentManagement.GraphQL.Queries.Types;

public sealed class DynamicContentTypeQueryBuilder : DynamicContentTypeBuilder
{
    public DynamicContentTypeQueryBuilder(IHttpContextAccessor httpContextAccessor,
        IOptions<GraphQLContentOptions> contentOptionsAccessor,
        IStringLocalizer<DynamicContentTypeQueryBuilder> localizer)
        : base(httpContextAccessor, contentOptionsAccessor, localizer)
    {
    }

    public override void Build(ISchema schema, FieldType contentQuery, ContentTypeDefinition contentTypeDefinition, ContentItemType contentItemType)
    {
        BuildInternal(schema, contentTypeDefinition, contentItemType);
    }
}
