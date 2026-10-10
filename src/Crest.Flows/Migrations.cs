using Crest.ContentManagement.Metadata;
using Crest.ContentManagement.Metadata.Settings;
using Crest.Data.Migration;
using Crest.Flows.Models;

namespace Crest.Flows;

public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync("FlowPart", builder => builder
            .Attachable()
            .WithDescription("Provides a customizable body for your content item where you can build a content structure with widgets."));

        await _contentDefinitionManager.AlterPartDefinitionAsync("BagPart", builder => builder
            .Attachable()
            .Reusable()
            .WithDescription("Provides a collection behavior for your content item where you can place other content items."));

        // Shortcut other migration steps on new content definition schemas.
        return 3;
    }

    // This code can be removed in a later version.
    public async Task<int> UpdateFrom1Async()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync("BagPart", builder => builder
            .Attachable()
            .Reusable()
            .WithDescription("Provides a collection behavior for your content item where you can place other content items."));

        return 2;
    }

    // Migrate PartSettings. This only needs to run on old content definition schemas.
    // This code can be removed in a later version.
    public async Task<int> UpdateFrom2Async()
    {
        await _contentDefinitionManager.MigratePartSettingsAsync<BagPart, BagPartSettings>();

        return 3;
    }
}
