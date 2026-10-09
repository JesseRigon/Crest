using Crest.ContentManagement.Metadata;
using Crest.ContentManagement.Metadata.Settings;
using Crest.Data.Migration;

namespace Crest.Search.Migrations;

public sealed class SearchMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public SearchMigrations(IContentDefinitionManager contentDefinitionManager)
        => _contentDefinitionManager = contentDefinitionManager;

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync("SearchFormPart", part => part
            .WithDisplayName("Search Form Part")
            .Attachable()
        );

        await _contentDefinitionManager.AlterTypeDefinitionAsync("SearchForm", type => type
            .Stereotype("Widget")
            .WithDisplayName("Search Form")
            .WithDescription("Provides a search form")
            .WithPart("SearchFormPart")
        );

        return 1;
    }
}
