using Grove.ArchiveLater.Indexes;
using Grove.ArchiveLater.Models;
using Grove.ContentManagement.Metadata;
using Grove.ContentManagement.Metadata.Settings;
using Grove.Data.Migration;
using YesSql.Sql;

namespace Grove.ArchiveLater;

public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(ArchiveLaterPart), builder => builder
            .Attachable()
            .WithDescription("Adds the ability to schedule content items to be archived at a given future date and time."));

        await SchemaBuilder.CreateMapIndexTableAsync<ArchiveLaterPartIndex>(table => table
            .Column<string>("ContentItemId")
            .Column<DateTime>("ScheduledArchiveDateTimeUtc")
            .Column<bool>("Published")
            .Column<bool>("Latest")
        );

        await SchemaBuilder.AlterIndexTableAsync<ArchiveLaterPartIndex>(table => table
            .CreateIndex("IDX_ArchiveLaterPartIndex_DocumentId",
                "Id",
                "DocumentId",
                "ContentItemId",
                "ScheduledArchiveDateTimeUtc",
                "Published",
                "Latest")
        );

        return 1;
    }
}
