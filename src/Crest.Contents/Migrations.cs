using Crest.ContentManagement.Metadata;
using Crest.ContentManagement.Metadata.Settings;
using Crest.Data.Migration;

namespace Crest.Contents;

public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync("CommonPart", builder => builder
            .Attachable()
            .WithDescription("Provides an editor for the common properties of a content item."));

        return 1;
    }
}
