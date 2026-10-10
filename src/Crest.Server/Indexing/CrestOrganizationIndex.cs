using Crest.ContentManagement;
using Crest.ContentManagement.Records;
using Crest.Data.Migration;
using Crest.Models;
using YesSql.Indexes;
using YesSql.Sql;

namespace Crest.Indexing;

public class CrestOrganizationIndex : MapIndex
{
    public string ContentItemId { get; set; }

    public string ContentType { get; set; }

    public string OrganizationId { get; set; }

    public bool Latest { get; set; }
}

public sealed class CrestOrganizationIndexProvider : IndexProvider<ContentItem>
{
    public override void Describe(DescribeContext<ContentItem> context)
    {
        context.For<CrestOrganizationIndex>()
            .Map(contentItem =>
            {
                // Only the latest version answers an authorization question.
                if (!contentItem.Latest)
                {
                    return null;
                }

                var part = contentItem.Get<CrestOrganizationPart>(nameof(CrestOrganizationPart));
                if (string.IsNullOrWhiteSpace(part?.OrganizationId))
                {
                    return null;
                }

                return new CrestOrganizationIndex
                {
                    ContentItemId = contentItem.ContentItemId,
                    ContentType = contentItem.ContentType,
                    OrganizationId = part.OrganizationId,
                    Latest = true,
                };
            });
    }
}

public sealed class CrestOrganizationIndexMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<CrestOrganizationIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("ContentType", column => column.WithLength(ContentItemIndex.MaxContentTypeSize))
            .Column<string>("OrganizationId", column => column.WithLength(26))
            .Column<bool>("Latest", column => column.Nullable()));

        await SchemaBuilder.AlterIndexTableAsync<CrestOrganizationIndex>(table => table
            .CreateIndex("IDX_CrestOrganizationIndex_DocumentId", "DocumentId"));

        await SchemaBuilder.AlterIndexTableAsync<CrestOrganizationIndex>(table => table
            .CreateIndex("IDX_CrestOrganizationIndex_Organization", "OrganizationId", "ContentType", "Latest"));

        return 1;
    }
}
