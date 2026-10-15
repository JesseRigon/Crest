using Grove.ContentManagement.Metadata;
using Grove.ContentManagement.Metadata.Settings;
using Grove.Data.Migration;

namespace Grove.Search.Migrations;

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

        // No "SearchForm" widget type: widgets are retired (docs/blazor-display.md, rulings
        // 2026-10-12); a search form is placed as a component by a template or a layer rule.

        return 1;
    }
}
