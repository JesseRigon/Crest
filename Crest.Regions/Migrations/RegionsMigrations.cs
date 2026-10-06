using Crest.Regions.Indexes;
using Crest.Regions.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace Crest.Regions.Migrations;

// Tenant-side schema: the Regional Profile type and the geo overlay index tables. The
// standard geo data is NOT here - it lives once, in the global store (GeoGlobalSchema).
// Fresh-install repeatable: CreateAsync is the whole target state.
public sealed class RegionsMigrations(IContentDefinitionManager contentDefinitionManager) : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await contentDefinitionManager.AlterPartDefinitionAsync(RegionsConstants.Parts.RegionalProfile, part => part
            .Attachable(false)
            .WithDisplayName("Regional profile")
            .WithDescription("A party's regional profile: default country, measurement system, time zone, language. Downstream modules attach their own facets."));

        await contentDefinitionManager.AlterTypeDefinitionAsync(RegionsConstants.ContentTypes.RegionalProfile, type => type
            .WithDisplayName("Regional profile")
            .Creatable()
            .Listable()
            .Securable()
            .Versionable()
            .WithPart("TitlePart", part => part.WithPosition("0"))
            .WithPart(RegionsConstants.Parts.RegionalProfile, part => part.WithPosition("1")));

        // The reference a party carries. Attachable so Parties (and anything else) can
        // add it to its own types; Crest attaches it to nothing.
        await contentDefinitionManager.AlterPartDefinitionAsync(RegionsConstants.Parts.RegionalProfileReference, part => part
            .Attachable()
            .Reusable(false)
            .WithDisplayName("Regional profile")
            .WithDescription("Which regional profile governs this party."));

        await SchemaBuilder.CreateMapIndexTableAsync<RegionalProfileIndex>(table => table
            .Column<string>(nameof(RegionalProfileIndex.ContentItemId), column => column.WithLength(26))
            .Column<string>(nameof(RegionalProfileIndex.Key), column => column.WithLength(64))
            .Column<string>(nameof(RegionalProfileIndex.DefaultCountry), column => column.Nullable().WithLength(8))
            .Column<bool>(nameof(RegionalProfileIndex.Enabled))
            .Column<bool>(nameof(RegionalProfileIndex.Published))
            .Column<bool>(nameof(RegionalProfileIndex.Latest)));
        await SchemaBuilder.AlterIndexTableAsync<RegionalProfileIndex>(table => table
            .CreateIndex("IDX_RegionalProfileIndex_Key", "DocumentId", "Key", "Published"));

        await SchemaBuilder.CreateMapIndexTableAsync<GeoTenantNodeIndex>(table => table
            .Column<string>(nameof(GeoTenantNodeIndex.NodeId), column => column.WithLength(64))
            .Column<string>(nameof(GeoTenantNodeIndex.Country), column => column.WithLength(8))
            .Column<int>(nameof(GeoTenantNodeIndex.Level))
            .Column<string>(nameof(GeoTenantNodeIndex.Kind), column => column.WithLength(16))
            .Column<string>(nameof(GeoTenantNodeIndex.Name), column => column.WithLength(255))
            .Column<bool>(nameof(GeoTenantNodeIndex.Orphaned)));
        await SchemaBuilder.AlterIndexTableAsync<GeoTenantNodeIndex>(table => table
            .CreateIndex("IDX_GeoTenantNodeIndex_NodeId", "DocumentId", "NodeId"));

        await SchemaBuilder.CreateMapIndexTableAsync<GeoTenantNodeParentIndex>(table => table
            .Column<string>(nameof(GeoTenantNodeParentIndex.NodeId), column => column.WithLength(64))
            .Column<string>(nameof(GeoTenantNodeParentIndex.ParentId), column => column.WithLength(64))
            .Column<string>(nameof(GeoTenantNodeParentIndex.Relation), column => column.WithLength(8)));
        await SchemaBuilder.AlterIndexTableAsync<GeoTenantNodeParentIndex>(table => table
            .CreateIndex("IDX_GeoTenantNodeParentIndex_Parent", "DocumentId", "ParentId", "Relation"));

        await SchemaBuilder.CreateMapIndexTableAsync<GeoNodeOverrideIndex>(table => table
            .Column<string>(nameof(GeoNodeOverrideIndex.NodeId), column => column.WithLength(64)));
        await SchemaBuilder.AlterIndexTableAsync<GeoNodeOverrideIndex>(table => table
            .CreateIndex("IDX_GeoNodeOverrideIndex_NodeId", "DocumentId", "NodeId"));

        return 1;
    }
}
