using System.Text.Json.Nodes;
using Crest.Global.Lists;
using Crest.Models;
using Crest.Regions;
using Crest.Regions.Fields;
using Crest.Services;
using Crest.Parties.Constants;
using Crest.Parties.Indexes;
using Crest.Parties.Services;
using OrchardCore.ContentFields.Settings;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Flows.Models;
using YesSql;
using YesSql.Sql;

namespace Crest.Parties.Migrations;

public sealed class PartiesMigrations(IContentDefinitionManager contentDefinitionManager) : DataMigration
{
    private const string GlobalPhoneCountryCodes = GlobalLists.PhoneCountryCodes;

    // Fresh-install repeatable: CreateAsync IS the target state. Pre-release, so there
    // are no UpdateFrom steps - a dev tenant is reset to reach a new shape.
    public async Task<int> CreateAsync()
    {
        await DefinePartyPartsAsync();
        await DefineContactElementsAsync();
        await DefineOrgPositionAsync();
        await DefinePartyTypesAsync();
        await DefineLocationAsync();
        await CreatePositionIndexAsync();
        await CreateAddressGeoIndexAsync();
        await CreateLocationIndexAsync();

        DeferAttachGlobalListPickers();
        DeferSeedPartyVocabularies();
        DeferAttachContactPickers();

        return 1;
    }

    // Person / Organization carry only what is intrinsic to the party. How to REACH a
    // party (email, phones, website, addresses) lives in the ContactPoints/Addresses
    // bags so a party can have any number of each, typed by tenant-editable kinds.
    private async Task DefinePartyPartsAsync()
    {
        await contentDefinitionManager.AlterPartDefinitionAsync(PartiesConstants.ContentTypes.Person, part => part
            .Attachable()
            .Reusable(false)
            .WithDisplayName("Person")
            .WithDescription("A human party profile. A person is not automatically an Orchard user account.")
            .WithField("FirstName", field => field
                .OfType("TextField")
                .WithDisplayName("First name")
                .WithPosition("0"))
            .WithField("LastName", field => field
                .OfType("TextField")
                .WithDisplayName("Last name")
                .WithPosition("1"))
            // The user <-> Person link, person side. A real user picker (indexed by
            // OrchardCore.ContentFields.Indexing.SQL.UserPicker) so "which person is
            // this account" is a query, not a scan.
            .WithField(PartyUserLinkService.PortalUserField, field => field
                .OfType("UserPickerField")
                .WithDisplayName("Portal user")
                .WithDescription("The Orchard user account that IS this person, once one exists (set by the module that creates the account).")
                .WithPosition("2")
                .MergeSettings<UserPickerFieldSettings>(settings =>
                {
                    settings.Multiple = false;
                    settings.DisplayAllUsers = true;
                })));

        await contentDefinitionManager.AlterPartDefinitionAsync(PartiesConstants.ContentTypes.Organization, part => part
            .Attachable()
            .Reusable(false)
            .WithDisplayName("Organization")
            .WithDescription("An organization party profile.")
            .WithField("TaxIdentifier", field => field
                .OfType("TextField")
                .WithDisplayName("Tax identifier")
                .WithPosition("0")));
    }

    // Bag element types. Their option pickers (Kind, PhoneCountry, Country, Region)
    // are attached in a deferred task because the lists they reference are seeded in
    // deferred tasks themselves.
    private async Task DefineContactElementsAsync()
    {
        await contentDefinitionManager.AlterPartDefinitionAsync(PartiesConstants.ContentTypes.ContactPoint, part => part
            .Attachable()
            .Reusable(false)
            .WithDisplayName("Contact point")
            .WithDescription("One way of reaching a party: an email address, a phone number, a website, a social handle.")
            .WithField("Value", field => field
                .OfType("TextField")
                .WithDisplayName("Value")
                .WithPosition("1"))
            .WithField("Label", field => field
                .OfType("TextField")
                .WithDisplayName("Label")
                .WithDescription("Optional free text: 'Work', 'After hours', 'Assistant'.")
                .WithPosition("2"))
            .WithField("Preferred", field => field
                .OfType("BooleanField")
                .WithDisplayName("Preferred")
                .WithDescription("The one to use among contact points of the same kind.")
                .WithPosition("4")));

        await contentDefinitionManager.AlterTypeDefinitionAsync(PartiesConstants.ContentTypes.ContactPoint, type => type
            .WithDisplayName("Contact point")
            .WithPart(PartiesConstants.ContentTypes.ContactPoint, part => part.WithPosition("0")));

        await contentDefinitionManager.AlterPartDefinitionAsync(PartiesConstants.ContentTypes.Address, part => part
            .Attachable()
            .Reusable(false)
            .WithDisplayName("Address")
            .WithDescription("A postal address of a party, typed by what it is used for.")
            .WithField("Line1", field => field
                .OfType("TextField")
                .WithDisplayName("Address line 1")
                .WithPosition("1"))
            .WithField("Line2", field => field
                .OfType("TextField")
                .WithDisplayName("Address line 2")
                .WithPosition("2"))
            .WithField("Locality", field => field
                .OfType("TextField")
                .WithDisplayName("City / locality")
                .WithDescription("The free-text city, post town or commune. What it is called comes from the country's addressing map.")
                .WithPosition("3"))
            // The machine half: the resolved geo stack, boundary memberships and point
            // (Crest.Regions). Level 1 of the stack is the country; the address's own
            // country selects its addressing map.
            .WithField("Geo", field => field
                .OfType(nameof(GeoStackField))
                .WithDisplayName("Geography")
                .WithPosition("5"))
            .WithField("PostalCode", field => field
                .OfType("TextField")
                .WithDisplayName("Postal code")
                .WithPosition("6"))
            .WithField("Preferred", field => field
                .OfType("BooleanField")
                .WithDisplayName("Preferred")
                .WithDescription("The one to use among addresses of the same kind.")
                .WithPosition("7")));

        await contentDefinitionManager.AlterTypeDefinitionAsync(PartiesConstants.ContentTypes.Address, type => type
            .WithDisplayName("Address")
            .WithPart(PartiesConstants.ContentTypes.Address, part => part.WithPosition("0")));
    }

    // Org structure: a Person holds positions in Organizations (many, in many). The
    // element references the organization with a stock ContentPickerField; because
    // bag-contained fields are not reached by stock field indexes, PartyPositionIndex
    // makes the organization-side lookup a query.
    private async Task DefineOrgPositionAsync()
    {
        await contentDefinitionManager.AlterPartDefinitionAsync(PartiesConstants.ContentTypes.OrgPosition, part => part
            .Attachable()
            .Reusable(false)
            .WithDisplayName("Organization position")
            .WithDescription("A position a person holds in an organization.")
            .WithField("Organization", field => field
                .OfType("ContentPickerField")
                .WithDisplayName("Organization")
                .WithPosition("0")
                .MergeSettings<ContentPickerFieldSettings>(settings =>
                {
                    settings.Multiple = false;
                    settings.DisplayedContentTypes = [PartiesConstants.ContentTypes.Organization];
                }))
            .WithField("Title", field => field
                .OfType("TextField")
                .WithDisplayName("Title")
                .WithPosition("1"))
            .WithField("Department", field => field
                .OfType("TextField")
                .WithDisplayName("Department")
                .WithPosition("2"))
            .WithField("Primary", field => field
                .OfType("BooleanField")
                .WithDisplayName("Primary")
                .WithDescription("The position to show first for this person.")
                .WithPosition("3")));

        await contentDefinitionManager.AlterTypeDefinitionAsync(PartiesConstants.ContentTypes.OrgPosition, type => type
            .WithDisplayName("Organization position")
            .WithPart(PartiesConstants.ContentTypes.OrgPosition, part => part.WithPosition("0")));
    }

    private async Task CreatePositionIndexAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<PartyPositionIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("PositionId", column => column.WithLength(26))
            .Column<string>("OrganizationId", column => column.WithLength(26))
            .Column<string>("Title", column => column.WithLength(255))
            .Column<bool>("IsPrimary")
            .Column<bool>("Published")
            .Column<bool>("Latest"));

        await SchemaBuilder.AlterIndexTableAsync<PartyPositionIndex>(table => table
            .CreateIndex("IDX_PartyPositionIndex_Organization", "DocumentId", "OrganizationId", "Latest", "Published"));
        await SchemaBuilder.AlterIndexTableAsync<PartyPositionIndex>(table => table
            .CreateIndex("IDX_PartyPositionIndex_ContentItemId", "DocumentId", "ContentItemId"));
    }

    // A Location is a place the tenant operates from. It carries physical fact only -
    // addresses, labelled points each with a geo stack and coordinates, floor area and
    // storeys - and never a tax field; the tax module attaches apportionment to this
    // type from its own migration (agents.md).
    private async Task DefineLocationAsync()
    {
        await contentDefinitionManager.AlterPartDefinitionAsync(PartiesConstants.ContentTypes.LocationPoint, part => part
            .Attachable()
            .Reusable(false)
            .WithDisplayName("Location point")
            .WithDescription("A register, dock, entrance or storey at a location, with its own geography.")
            .WithField("Label", field => field
                .OfType("TextField")
                .WithDisplayName("Label")
                .WithPosition("0"))
            .WithField("Geo", field => field
                .OfType(nameof(GeoStackField))
                .WithDisplayName("Geography")
                .WithPosition("1"))
            .WithField("FloorArea", field => field
                .OfType("NumericField")
                .WithDisplayName("Floor area")
                .WithDescription("In the tenant's measurement system.")
                .WithPosition("2")
                .MergeSettings<NumericFieldSettings>(settings => settings.Scale = 2))
            .WithField("Storey", field => field
                .OfType("NumericField")
                .WithDisplayName("Storey")
                .WithPosition("3")
                .MergeSettings<NumericFieldSettings>(settings => settings.Scale = 0)));

        await contentDefinitionManager.AlterTypeDefinitionAsync(PartiesConstants.ContentTypes.LocationPoint, type => type
            .WithDisplayName("Location point")
            .WithPart(PartiesConstants.ContentTypes.LocationPoint, part => part.WithPosition("0")));

        await contentDefinitionManager.AlterPartDefinitionAsync(PartiesConstants.ContentTypes.Location, part => part
            .Attachable(false)
            .WithDisplayName("Location")
            .WithDescription("A place the tenant operates from.")
            .WithField("IsPrimary", field => field
                .OfType("BooleanField")
                .WithDisplayName("Primary")
                .WithDescription("The tenant's home: its covering tax authorities set the standard rate for unclassified items.")
                .WithPosition("0"))
            .WithField("FloorArea", field => field
                .OfType("NumericField")
                .WithDisplayName("Floor area")
                .WithPosition("1")
                .MergeSettings<NumericFieldSettings>(settings => settings.Scale = 2))
            .WithField("Storeys", field => field
                .OfType("NumericField")
                .WithDisplayName("Storeys")
                .WithPosition("2")
                .MergeSettings<NumericFieldSettings>(settings => settings.Scale = 0)));

        await contentDefinitionManager.AlterTypeDefinitionAsync(PartiesConstants.ContentTypes.Location, type => type
            .WithDisplayName("Location")
            .Creatable()
            .Listable()
            .Draftable()
            .Versionable()
            .Securable()
            .WithPart("TitlePart", part => part.WithPosition("0"))
            .WithPart(PartiesConstants.ContentTypes.Location, part => part.WithPosition("1"))
            .WithPart(PartiesConstants.Bags.Addresses, "BagPart", part => part
                .WithDisplayName("Addresses")
                .WithPosition("2")
                .WithSettings(new BagPartSettings { ContainedContentTypes = [PartiesConstants.ContentTypes.Address] }))
            .WithPart(PartiesConstants.Bags.Points, "BagPart", part => part
                .WithDisplayName("Points")
                .WithPosition("3")
                .WithSettings(new BagPartSettings { ContainedContentTypes = [PartiesConstants.ContentTypes.LocationPoint] })));
    }

    private async Task CreateLocationIndexAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<LocationIndex>(table => table
            .Column<string>(nameof(LocationIndex.ContentItemId), column => column.WithLength(26))
            .Column<bool>(nameof(LocationIndex.IsPrimary))
            .Column<bool>(nameof(LocationIndex.Published))
            .Column<bool>(nameof(LocationIndex.Latest)));
        await SchemaBuilder.AlterIndexTableAsync<LocationIndex>(table => table
            .CreateIndex("IDX_LocationIndex_Primary", "DocumentId", "IsPrimary", "Published"));
    }

    // Addresses are bag-contained, so their geo stack is not reached by stock field
    // indexes; this many-row index (one row per node) is what tax and territory lookups
    // query - and what "no level columns" means concretely.
    private async Task CreateAddressGeoIndexAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<AddressGeoNodeIndex>(table => table
            .Column<string>(nameof(AddressGeoNodeIndex.PartyId), column => column.WithLength(26))
            .Column<string>(nameof(AddressGeoNodeIndex.AddressId), column => column.WithLength(26))
            .Column<string>(nameof(AddressGeoNodeIndex.NodeId), column => column.WithLength(64))
            .Column<int>(nameof(AddressGeoNodeIndex.Level))
            .Column<string>(nameof(AddressGeoNodeIndex.KindId), column => column.Nullable().WithLength(64))
            .Column<bool>(nameof(AddressGeoNodeIndex.Latest)));
        await SchemaBuilder.AlterIndexTableAsync<AddressGeoNodeIndex>(table => table
            .CreateIndex("IDX_AddressGeoNodeIndex_Node", "DocumentId", "NodeId", "Latest"));
        await SchemaBuilder.AlterIndexTableAsync<AddressGeoNodeIndex>(table => table
            .CreateIndex("IDX_AddressGeoNodeIndex_Address", "DocumentId", "AddressId"));
    }

    private async Task DefinePartyTypesAsync()
    {
        await contentDefinitionManager.AlterTypeDefinitionAsync(PartiesConstants.ContentTypes.Person, type => type
            .WithDisplayName("Person")
            .Creatable()
            .Listable()
            .Draftable()
            .Versionable()
            .Securable()
            .WithPart("TitlePart", part => part.WithPosition("0"))
            .WithPart(PartiesConstants.ContentTypes.Person, part => part.WithPosition("1"))
            // Parties attaches the context reference; Crest never names Person (agents.md).
            .WithPart(RegionsConstants.Parts.LocalizationProfileReference, part => part.WithPosition("5"))
            .WithPart(PartiesConstants.Bags.ContactPoints, "BagPart", part => part
                .WithDisplayName("Contact points")
                .WithPosition("2")
                .WithSettings(new BagPartSettings { ContainedContentTypes = [PartiesConstants.ContentTypes.ContactPoint] }))
            .WithPart(PartiesConstants.Bags.Addresses, "BagPart", part => part
                .WithDisplayName("Addresses")
                .WithPosition("3")
                .WithSettings(new BagPartSettings { ContainedContentTypes = [PartiesConstants.ContentTypes.Address] }))
            .WithPart(PartiesConstants.Bags.Positions, "BagPart", part => part
                .WithDisplayName("Positions")
                .WithPosition("4")
                .WithSettings(new BagPartSettings { ContainedContentTypes = [PartiesConstants.ContentTypes.OrgPosition] })));

        await contentDefinitionManager.AlterTypeDefinitionAsync(PartiesConstants.ContentTypes.Organization, type => type
            .WithDisplayName("Organization")
            .Creatable()
            .Listable()
            .Draftable()
            .Versionable()
            .Securable()
            .WithPart("TitlePart", part => part.WithPosition("0"))
            .WithPart(PartiesConstants.ContentTypes.Organization, part => part.WithPosition("1"))
            .WithPart(RegionsConstants.Parts.LocalizationProfileReference, part => part.WithPosition("5"))
            .WithPart(PartiesConstants.Bags.ContactPoints, "BagPart", part => part
                .WithDisplayName("Contact points")
                .WithPosition("2")
                .WithSettings(new BagPartSettings { ContainedContentTypes = [PartiesConstants.ContentTypes.ContactPoint] }))
            .WithPart(PartiesConstants.Bags.Addresses, "BagPart", part => part
                .WithDisplayName("Addresses")
                .WithPosition("3")
                .WithSettings(new BagPartSettings { ContainedContentTypes = [PartiesConstants.ContentTypes.Address] })));
    }

    // The party-domain vocabularies this module OWNS (the cross-domain ones - honorifics,
    // languages, countries - stay Crest global lists). DEFERRED, verified the hard way
    // (2026-09-09): Parties migrates EARLY in the dependency order, and creating
    // content items inline in CreateAsync during first-time setup fires other
    // features' handlers against index tables that do not exist yet
    // (ContentItemIndex, workflow indexes) and aborts provisioning. Accounting's
    // inline seeds only survive because Accounting migrates after Contents; do not
    // copy that shape into early modules.
    //
    // None are locked: no shipped logic branches on them yet, and tenants extend them
    // freely. When posting starts snapshotting addresses by kind, the address-kind
    // seed gains a DataLock and a reseed re-asserts it.
    private static void DeferSeedPartyVocabularies()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var contentPartLists = scope.ServiceProvider.GetRequiredService<ICrestContentPartListService>();

            await SeedPartyVocabulariesAsync(contentPartLists);
        });
    }

    private static async Task SeedPartyVocabulariesAsync(ICrestContentPartListService contentPartLists)
    {
        await contentPartLists.SeedAsync(new CrestContentPartListSeed(
            PartiesOptionSets.ContactPointKind,
            "Contact point kind",
            [
                new(ContactPointKinds.Email, "Email"),
                new(ContactPointKinds.Phone, "Phone"),
                new(ContactPointKinds.Mobile, "Mobile"),
                new(ContactPointKinds.Fax, "Fax"),
                new(ContactPointKinds.Website, "Website"),
                new(ContactPointKinds.Social, "Social"),
                new(ContactPointKinds.Messaging, "Messaging"),
                new(ContactPointKinds.Other, "Other"),
            ]));

        await contentPartLists.SeedAsync(new CrestContentPartListSeed(
            PartiesOptionSets.AddressKind,
            "Address kind",
            [
                new(AddressKinds.Billing, "Billing"),
                new(AddressKinds.Shipping, "Shipping"),
                new(AddressKinds.Mailing, "Mailing"),
                new(AddressKinds.Physical, "Physical"),
                new(AddressKinds.Other, "Other"),
            ]));

        // Values from the enterprise outline's OrganizationType enum table.
        await contentPartLists.SeedAsync(new CrestContentPartListSeed(
            PartiesOptionSets.OrganizationType,
            "Organization type",
            [
                new("incorporation", "Incorporation"),
                new("llc", "LLC"),
                new("dba", "DBA"),
                new("community-group", "Community group"),
                new("ngo", "NGO"),
                new("gov", "Government"),
            ]));

        // A neutral starter set; the outline only says "its own industry list table",
        // so tenants are expected to shape this one to their vertical.
        await contentPartLists.SeedAsync(new CrestContentPartListSeed(
            PartiesOptionSets.Industry,
            "Industry",
            [
                new("agriculture", "Agriculture"),
                new("construction", "Construction"),
                new("education", "Education"),
                new("finance", "Finance"),
                new("government", "Government"),
                new("healthcare", "Healthcare"),
                new("hospitality", "Hospitality"),
                new("manufacturing", "Manufacturing"),
                new("nonprofit", "Nonprofit"),
                new("professional-services", "Professional services"),
                new("real-estate", "Real estate"),
                new("retail", "Retail"),
                new("technology", "Technology"),
                new("transportation", "Transportation"),
                new("wholesale", "Wholesale"),
                new("other", "Other"),
            ]));

        await contentPartLists.AttachToContentTypeAsync(
            PartiesOptionSets.OrganizationType, PartiesConstants.ContentTypes.Organization, "OrganizationType", "Organization type");
        await contentPartLists.AttachToContentTypeAsync(
            PartiesOptionSets.Industry, PartiesConstants.ContentTypes.Organization, "Industry", "Industry");
    }

    // Pickers on the bag element types. Registered AFTER the vocabulary seed so the
    // party-owned kind lists exist; the global lists were seeded by Crest's own
    // migration earlier in the deferred queue.
    private static void DeferAttachContactPickers()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var contentPartLists = scope.ServiceProvider.GetRequiredService<ICrestContentPartListService>();

            await contentPartLists.AttachToContentTypeAsync(
                PartiesOptionSets.ContactPointKind, PartiesConstants.ContentTypes.ContactPoint, "Kind", "Kind",
                settings => settings.Required = true);
            await contentPartLists.AttachToContentTypeAsync(
                GlobalPhoneCountryCodes, PartiesConstants.ContentTypes.ContactPoint, "PhoneCountry", "Phone country");

            await contentPartLists.AttachToContentTypeAsync(
                PartiesOptionSets.AddressKind, PartiesConstants.ContentTypes.Address, "Kind", "Kind",
                settings => settings.Required = true);
        });
    }

    // Tenants provisioned before the global reference lists were wired in.
    // Person's name furniture and language come from Crest's shared GLOBAL lists
    // (seeded by the Crest.ContentPartLists feature itself) rather than lists this
    // module owns - honorifics, name suffixes and languages cross every domain
    // boundary.
    //
    // DEFERRED, because AttachToContentTypeAsync resolves the target list and the
    // global lists are themselves seeded in a deferred task (content creation during
    // first-time setup cannot run inline - see plans/global.md).
    // Deferred tasks run in registration order and feature dependencies put Crest's
    // migration first, so the lists exist by the time this attaches to them.
    private static void DeferAttachGlobalListPickers()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var contentPartLists = scope.ServiceProvider.GetRequiredService<ICrestContentPartListService>();

            await contentPartLists.AttachToContentTypeAsync(GlobalLists.Honorifics, PartiesConstants.ContentTypes.Person, "Honorific", "Honorific");
            await contentPartLists.AttachToContentTypeAsync(GlobalLists.NameSuffixes, PartiesConstants.ContentTypes.Person, "Suffix", "Suffix");
            await contentPartLists.AttachToContentTypeAsync(GlobalLists.Languages, PartiesConstants.ContentTypes.Person, "PreferredLanguage", "Preferred language");
        });
    }
}
