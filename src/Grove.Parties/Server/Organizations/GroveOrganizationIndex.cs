using Grove.ContentManagement;
using Grove.ContentManagement.Records;
using Grove.Data.Migration;
using YesSql.Indexes;
using YesSql.Sql;

namespace Grove.Parties.Organizations;

public class GroveOrganizationIndex : MapIndex
{
    public string ContentItemId { get; set; }

    public string ContentType { get; set; }

    public string OrganizationId { get; set; }

    public bool Latest { get; set; }
}

public sealed class OrganizationIndexProvider : IndexProvider<ContentItem>
{
    public override void Describe(DescribeContext<ContentItem> context)
    {
        context.For<GroveOrganizationIndex>()
            .Map(contentItem =>
            {
                // Only the latest version answers an authorization question.
                if (!contentItem.Latest)
                {
                    return null;
                }

                var part = contentItem.Get<GroveOrganizationPart>(nameof(GroveOrganizationPart));
                if (string.IsNullOrWhiteSpace(part?.OrganizationId))
                {
                    return null;
                }

                return new GroveOrganizationIndex
                {
                    ContentItemId = contentItem.ContentItemId,
                    ContentType = contentItem.ContentType,
                    OrganizationId = part.OrganizationId,
                    Latest = true,
                };
            });
    }
}

public sealed class GroveOrganizationIndexMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<GroveOrganizationIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("ContentType", column => column.WithLength(ContentItemIndex.MaxContentTypeSize))
            .Column<string>("OrganizationId", column => column.WithLength(26))
            .Column<bool>("Latest", column => column.Nullable()));

        await SchemaBuilder.AlterIndexTableAsync<GroveOrganizationIndex>(table => table
            .CreateIndex("IDX_GroveOrganizationIndex_DocumentId", "DocumentId"));

        await SchemaBuilder.AlterIndexTableAsync<GroveOrganizationIndex>(table => table
            .CreateIndex("IDX_GroveOrganizationIndex_Organization", "OrganizationId", "ContentType", "Latest"));

        return 1;
    }
}
