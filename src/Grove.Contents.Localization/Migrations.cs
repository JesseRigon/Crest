using Microsoft.Extensions.DependencyInjection;
using Grove.Contents.Localization.Models;
using Grove.Contents.Management;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Settings;
using Grove.Data.Migration;
using Grove.Environment.Shell.Scope;
using YesSql;
using YesSql.Sql;
using Grove.Contents.Types;

namespace Grove.Contents.Localization.Records;

public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterFieldSetDefinitionAsync(ContentFieldSetName.Of<LocalizationFieldSet>(), builder => builder
            .Attachable()
            .WithDescription("Provides a way to create localized version of content."));

        await SchemaBuilder.CreateMapIndexTableAsync<LocalizedContentItemIndex>(table => table
            .Column<string>("LocalizationSet", col => col.WithLength(26))
            .Column<string>("Culture", col => col.WithLength(16))
            .Column<string>("ContentItemId", c => c.WithLength(26))
            .Column<bool>("Published")
            .Column<bool>("Latest")
        );

        await SchemaBuilder.AlterIndexTableAsync<LocalizedContentItemIndex>(table => table
            .CreateIndex("IDX_LocalizationIndex_DocumentId",
            "DocumentId",
            "LocalizationSet",
            "Culture",
            "ContentItemId",
            "Published",
            "Latest")
        );

        await DefineLocalizationFieldsAsync();

        // Shortcut other migration steps on new content definition schemas.
        return 5;
    }

    // This code can be removed in a later version.
    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<LocalizedContentItemIndex>(table => table
            .AddColumn<bool>(nameof(LocalizedContentItemIndex.Published)));

        return 2;
    }

    // This code can be removed in a later version.
    public async Task<int> UpdateFrom2Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<LocalizedContentItemIndex>(table => table
            .AddColumn<bool>(nameof(LocalizedContentItemIndex.Latest))
        );

        await SchemaBuilder.AlterIndexTableAsync<LocalizedContentItemIndex>(table => table
            .CreateIndex("IDX_LocalizationIndex_DocumentId",
            "DocumentId",
            "LocalizationSet",
            "Culture",
            "ContentItemId",
            "Published",
            "Latest")
        );

        return 3;
    }

    // Migrate null LocalizedContentItemIndex Latest column.
    public static int UpdateFrom3()
    {
        // Defer this until after the subsequent migrations have succeeded as the schema has changed.
        ShellScope.AddDeferredTask(async scope =>
        {
            var session = scope.ServiceProvider.GetRequiredService<ISession>();
            var localizedContentItems = await session.Query<ContentItem, LocalizedContentItemIndex>().ListAsync();

            foreach (var localizedContentItem in localizedContentItems)
            {
                localizedContentItem.Latest = localizedContentItem.ContentItem.Latest;
                await session.SaveAsync(localizedContentItem);
            }
        });

        return 4;
    }

    // The field set's values are fields (docs/content-engine.md › Behaviour is workflows); the ones its
    // system flows require are locked by the module.
    public async Task<int> UpdateFrom4Async()
    {
        await DefineLocalizationFieldsAsync();
        return 5;
    }

    private Task DefineLocalizationFieldsAsync() =>
        _contentDefinitionManager.AlterFieldSetDefinitionAsync(ContentFieldSetName.Of<LocalizationFieldSet>(), builder => builder
            .WithField(LocalizationFieldSet.CultureField, field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Culture")
                .WithPosition("0")
                .WithEditor(ContentFieldEditors.None)
                .Locked())
            .WithField(LocalizationFieldSet.LocalizationSetField, field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Localization set")
                .WithPosition("1")
                .WithEditor(ContentFieldEditors.None)
                .Locked()));
}
