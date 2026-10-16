using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Settings;
using Grove.Data.Migration;

namespace Grove.Contents.Preview;

public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterFieldSetDefinitionAsync("Preview", builder => builder
            .Attachable()
            .WithDescription("Provides a way to define the url that is used to render your content item for preview. You only need to use this for the content preview feature when running the frontend decoupled from the admin."));

        return 1;
    }
}
