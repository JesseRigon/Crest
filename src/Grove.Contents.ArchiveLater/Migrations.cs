using Grove.Contents.ArchiveLater.Indexes;
using Grove.Contents.ArchiveLater.Models;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Settings;
using Grove.Data.Migration;
using YesSql.Sql;
using Grove.Contents.Management;
using Grove.Contents.Types;

namespace Grove.Contents.ArchiveLater;

public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterFieldSetDefinitionAsync(ContentFieldSetName.Of<ArchiveLaterFieldSet>(), builder => builder
            .Attachable()
            .WithDescription("Adds the ability to schedule content items to be archived at a given future date and time."));

        await SchemaBuilder.CreateMapIndexTableAsync<ArchiveLaterIndex>(table => table
            .Column<string>("ContentItemId")
            .Column<DateTime>("ScheduledArchiveDateTimeUtc")
            .Column<bool>("Published")
            .Column<bool>("Latest")
        );

        await SchemaBuilder.AlterIndexTableAsync<ArchiveLaterIndex>(table => table
            .CreateIndex("IDX_ArchiveLaterIndex_DocumentId",
                "Id",
                "DocumentId",
                "ContentItemId",
                "ScheduledArchiveDateTimeUtc",
                "Published",
                "Latest")
        );

        await DefineArchiveLaterFieldsAsync();

        return 2;
    }

    // The field set's values are fields (docs/content-engine.md › Behaviour is workflows); the ones its
    // system flows require are locked by the module.
    public async Task<int> UpdateFrom1Async()
    {
        await DefineArchiveLaterFieldsAsync();
        return 2;
    }

    private Task DefineArchiveLaterFieldsAsync() =>
        _contentDefinitionManager.AlterFieldSetDefinitionAsync("ArchiveLater", builder => builder
            .WithField(Models.ArchiveLaterFieldSet.ScheduledArchiveUtcField, field => field
                .OfType("DateTimeField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Archive at")
                .WithPosition("0")
                .Locked()));
}
