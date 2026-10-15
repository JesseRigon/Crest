using Grove.ContentManagement.Metadata;
using Grove.Data.Migration;

namespace Grove.Demo;

public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterTypeDefinitionAsync("Foo", builder => builder
            .WithPart("TestContentPartA")
            .WithPart("TestContentPartB")
        );

        return 1;
    }
}
