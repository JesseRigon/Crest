using Grove.Models;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Settings;
using Grove.Data.Migration;
using Grove.Contents.Management;

namespace Grove.Migrations;

// Option lists - Grove's tenant-editable enum system.
//
// FULLY PARALLEL to Grove's Taxonomy/Term, sharing no parts with it. Stock Taxonomy
// is a CMS categorization feature: routable (AliasFieldSet + AutorouteFieldSet, term pages,
// Liquid shapes), and everything carrying TaxonomyFieldSet is listed under Configuration >
// Taxonomies. Configuration data wants none of that, and the word is jargon to
// tenants - so these types carry their own option storage, and Grove takes NO
// dependency on Grove.Contents.Taxonomies.
//
// Nothing here modifies Grove; the types are ordinary content types built from
// stock building blocks.
public sealed class OptionListMigrations(IContentDefinitionManager contentDefinitionManager) : DataMigration
{
    public const string OptionListContentType = "OptionList";
    public const string OptionContentType = "Option";

    public async Task<int> CreateAsync()
    {
        await contentDefinitionManager.AlterFieldSetDefinitionAsync(ContentFieldSetName.Of<OptionListFieldSet>(), part => part
            .Attachable()
            .Reusable(false)
            .WithDisplayName("Option list")
            .WithDescription("A named, tenant-editable set of options that content types can reference."));

        await contentDefinitionManager.AlterFieldSetDefinitionAsync(ContentFieldSetName.Of<OptionEntryFieldSet>(), part => part
            .Attachable()
            .Reusable(false)
            .WithDisplayName("Option")
            .WithDescription("A member of an option list. The key is what code matches on; the title is the editable label."));

        await contentDefinitionManager.AlterTypeDefinitionAsync(OptionContentType, type => type
            .WithDisplayName("Option")
            .Versionable()
            .Securable()
            // Options exist inside their list, never standalone: not Creatable, not
            // Listable - they are reached through their Option list.
            .WithFieldSet("Title", part => part.WithPosition("0"))
            .WithFieldSet(ContentFieldSetName.Of<OptionEntryFieldSet>(), part => part.WithPosition("1")));

        await contentDefinitionManager.AlterTypeDefinitionAsync(OptionListContentType, type => type
            .WithDisplayName("Option list")
            .Creatable()
            .Listable()
            .Versionable()
            .Securable()
            // OptionListFieldSet carries the options themselves (contained in this
            // item's own document), so no taxonomy part is involved.
            .WithFieldSet("Title", part => part.WithPosition("0"))
            .WithFieldSet(ContentFieldSetName.Of<OptionListFieldSet>(), part => part.WithPosition("1")));

        return 3;
    }

    // Tenants created against the first pass have TaxonomyFieldSet stored on their
    // OptionList definition. Content type definitions live in the database, so
    // rewriting CreateAsync alone would leave existing tenants coupled to the
    // Taxonomies module forever - this drops the part where it was already recorded.
    public async Task<int> UpdateFrom1Async()
    {
        await contentDefinitionManager.AlterTypeDefinitionAsync(OptionListContentType, type => type
            .RemoveFieldSet("Taxonomy"));

        return 2;
    }

    // The list type's display name drifted from its part ("Option list") when
    // the feature was renamed; re-apply it so existing tenants stop reading "Option list".
    public async Task<int> UpdateFrom2Async()
    {
        await contentDefinitionManager.AlterTypeDefinitionAsync(OptionListContentType, type => type
            .WithDisplayName("Option list"));

        return 3;
    }
}
