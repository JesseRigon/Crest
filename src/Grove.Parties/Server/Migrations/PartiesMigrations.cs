using System.Text.Json.Nodes;
using Grove.Data.Global.Lists;
using Grove.Models;
using Grove.Regions;
using Grove.Regions.Fields;
using Grove.Services;
using Grove.Parties.Constants;
using Grove.Parties.Indexes;
using Grove.Parties.Services;
using Grove.ContentFields.Settings;
using Microsoft.Extensions.DependencyInjection;
using Grove.ContentManagement;
using Grove.ContentManagement.Metadata;
using Grove.ContentManagement.Metadata.Settings;
using Grove.ContentManagement.Records;
using Grove.Data.Migration;
using Grove.Environment.Shell.Scope;
using Grove.Flows.Models;
using YesSql;
using YesSql.Sql;
using Grove.Fields;
using Grove.Settings;

namespace Grove.Parties.Migrations;

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
        await DefineOrganizationNameAsync();
        await DefinePartyTypesAsync();
        await DefineAssociationAsync();
        await DefineLocationAsync();
        await CreatePositionIndexAsync();
        await CreateAddressGeoIndexAsync();
        await CreateLocationIndexAsync();
        await CreateContactPointIndexAsync();
        await CreateOrganizationNameIndexAsync();
        await CreateAssociationIndexAsync();

        DeferAttachGlobalListPickers();
        DeferSeedPartyVocabularies();
        DeferAttachContactPickers();
        DeferEnsureTenantOrganization();

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
            .WithDescription("A human party profile. A person is not automatically an Grove user account.")
            .WithField("FirstName", field => field
                .OfType("TextField")
                .WithDisplayName("First name")
                .WithPosition("0"))
            .WithField("LastName", field => field
                .OfType("TextField")
                .WithDisplayName("Last name")
                .WithPosition("1"))
            // The user <-> Person link, person side. A real user picker (indexed by
            // Grove.ContentFields.Indexing.SQL.UserPicker) so "which person is
            // this account" is a query, not a scan.
            .WithField(PartyUserLinkService.PortalUserField, field => field
                .OfType("UserPickerField")
                .WithDisplayName("Portal user")
                .WithDescription("The Grove user account that IS this person, once one exists (set by the module that creates the account).")
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
                .WithPosition("0"))
            // The organization this one belongs to. Restricted to Organization and
            // guarded on every save path: the picker's allowed types are enforced by the
            // content picker's save guard, the cycle refusal by Parties' relationship
            // handler (ParentOrganizationRelationshipHandler).
            // An Individual organization: the organization of one person in a business
            // relationship (a regular customer), created when an organization role is added
            // to a person; exactly one member; converted to an ordinary one only when the
            // tenant allows (docs/parties.md, ruling 2026-10-14).
            .WithField(PartiesConstants.OrganizationFields.Individual, field => field
                .OfType("BooleanField")
                .WithDisplayName("Individual")
                .WithDescription("The organization of one person; it reads as the person.")
                .WithPosition("2"))
            .WithField(PartiesConstants.OrganizationFields.ParentOrganization, field => field
                .OfType("OptionPickerField")
                .WithDisplayName("Parent organization")
                .WithPosition("1")
                .MergeSettings<OptionPickerFieldSettings>(settings =>
                {
                    settings.Multiple = false;
                    settings.SourceKey = OptionSourceKeys.ForContentItems([PartiesConstants.ContentTypes.Organization]);
                })));
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
            // (Grove.Regions). Level 1 of the stack is the country; the address's own
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
    // element references the organization with an option picker over content items; because
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
                .OfType("OptionPickerField")
                .WithDisplayName("Organization")
                .WithPosition("0")
                .MergeSettings<OptionPickerFieldSettings>(settings =>
                {
                    settings.Multiple = false;
                    settings.SourceKey = OptionSourceKeys.ForContentItems([PartiesConstants.ContentTypes.Organization]);
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

    // An organization's legal entity names: a kinded bag like contact points, so an
    // organization holds any number of names (several DBAs), one primary per kind. None
    // of them is a display name; the title stays the name shown (docs/parties.md).
    private async Task DefineOrganizationNameAsync()
    {
        await contentDefinitionManager.AlterPartDefinitionAsync(PartiesConstants.ContentTypes.OrganizationName, part => part
            .Attachable()
            .Reusable(false)
            .WithDisplayName("Legal entity name")
            .WithDescription("A legal name, trade name (DBA) or former name of an organization.")
            .WithField("Name", field => field
                .OfType("TextField")
                .WithDisplayName("Name")
                .WithPosition("1"))
            .WithField("Primary", field => field
                .OfType("BooleanField")
                .WithDisplayName("Primary")
                .WithDescription("The one to use among names of the same kind.")
                .WithPosition("2")));

        await contentDefinitionManager.AlterTypeDefinitionAsync(PartiesConstants.ContentTypes.OrganizationName, type => type
            .WithDisplayName("Legal entity name")
            .WithPart(PartiesConstants.ContentTypes.OrganizationName, part => part.WithPosition("0")));
    }

    // An association: a typed connection between two parties beside reports-to (sales
    // rep to contact, account rep to organization), possibly crossing staff and an
    // organization. A content item, so editing, versioning, permissions and audit come
    // from the content system; kept apart from the hierarchy (docs/parties.md). Each end
    // is a person and the organization they act in; the Type picker is attached in the
    // deferred vocabulary task.
    private async Task DefineAssociationAsync()
    {
        await contentDefinitionManager.AlterPartDefinitionAsync(PartiesConstants.ContentTypes.Association, part => part
            .Attachable(false)
            .WithDisplayName("Association")
            .WithDescription("A typed connection between two parties that is not reports-to.")
            .WithField(AssociationFields.FromPerson, field => PersonPicker(field, "From person", "1"))
            .WithField(AssociationFields.FromOrganization, field => OrganizationPicker(field, "From organization", "2"))
            .WithField(AssociationFields.ToPerson, field => PersonPicker(field, "To person", "3"))
            .WithField(AssociationFields.ToOrganization, field => OrganizationPicker(field, "To organization", "4")));

        await contentDefinitionManager.AlterTypeDefinitionAsync(PartiesConstants.ContentTypes.Association, type => type
            .WithDisplayName("Association")
            .Creatable()
            .Listable()
            .Versionable()
            .Securable()
            .WithPart("TitlePart", part => part.WithPosition("0"))
            .WithPart(PartiesConstants.ContentTypes.Association, part => part.WithPosition("1")));

        static Grove.ContentManagement.Metadata.Builders.ContentPartFieldDefinitionBuilder PersonPicker(Grove.ContentManagement.Metadata.Builders.ContentPartFieldDefinitionBuilder field, string name, string position) => field
            .OfType("OptionPickerField")
            .WithDisplayName(name)
            .WithPosition(position)
            .MergeSettings<OptionPickerFieldSettings>(settings =>
            {
                settings.Multiple = false;
                settings.SourceKey = OptionSourceKeys.ForContentItems([PartiesConstants.ContentTypes.Person]);
            });

        static Grove.ContentManagement.Metadata.Builders.ContentPartFieldDefinitionBuilder OrganizationPicker(Grove.ContentManagement.Metadata.Builders.ContentPartFieldDefinitionBuilder field, string name, string position) => field
            .OfType("OptionPickerField")
            .WithDisplayName(name)
            .WithPosition(position)
            .MergeSettings<OptionPickerFieldSettings>(settings =>
            {
                settings.Multiple = false;
                settings.SourceKey = OptionSourceKeys.ForContentItems([PartiesConstants.ContentTypes.Organization]);
            });
    }

    private async Task CreateContactPointIndexAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ContactPointIndex>(table => table
            .Column<string>(nameof(ContactPointIndex.PartyId), column => column.WithLength(26))
            .Column<string>(nameof(ContactPointIndex.PartyContentType), column => column.WithLength(255))
            .Column<string>(nameof(ContactPointIndex.ContactPointId), column => column.WithLength(26))
            .Column<string>(nameof(ContactPointIndex.KindId), column => column.Nullable().WithLength(64))
            .Column<string>(nameof(ContactPointIndex.Value), column => column.Nullable().WithLength(255))
            .Column<bool>(nameof(ContactPointIndex.Preferred))
            .Column<bool>(nameof(ContactPointIndex.Published))
            .Column<bool>(nameof(ContactPointIndex.Latest)));
        await SchemaBuilder.AlterIndexTableAsync<ContactPointIndex>(table => table
            .CreateIndex("IDX_ContactPointIndex_Kind", "DocumentId", "KindId", "Preferred", "Published", "Latest"));
        await SchemaBuilder.AlterIndexTableAsync<ContactPointIndex>(table => table
            .CreateIndex("IDX_ContactPointIndex_Party", "DocumentId", "PartyId"));
    }

    private async Task CreateOrganizationNameIndexAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<OrganizationNameIndex>(table => table
            .Column<string>(nameof(OrganizationNameIndex.OrganizationId), column => column.WithLength(26))
            .Column<string>(nameof(OrganizationNameIndex.NameId), column => column.WithLength(26))
            .Column<string>(nameof(OrganizationNameIndex.KindId), column => column.Nullable().WithLength(64))
            .Column<string>(nameof(OrganizationNameIndex.Name), column => column.Nullable().WithLength(255))
            .Column<bool>(nameof(OrganizationNameIndex.Primary))
            .Column<bool>(nameof(OrganizationNameIndex.Published))
            .Column<bool>(nameof(OrganizationNameIndex.Latest)));
        await SchemaBuilder.AlterIndexTableAsync<OrganizationNameIndex>(table => table
            .CreateIndex("IDX_OrganizationNameIndex_Kind", "DocumentId", "KindId", "Primary", "Published", "Latest"));
        await SchemaBuilder.AlterIndexTableAsync<OrganizationNameIndex>(table => table
            .CreateIndex("IDX_OrganizationNameIndex_Organization", "DocumentId", "OrganizationId"));
    }

    private async Task CreateAssociationIndexAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<AssociationIndex>(table => table
            .Column<string>(nameof(AssociationIndex.ContentItemId), column => column.WithLength(26))
            .Column<string>(nameof(AssociationIndex.TypeId), column => column.Nullable().WithLength(64))
            .Column<string>(nameof(AssociationIndex.FromPersonId), column => column.Nullable().WithLength(26))
            .Column<string>(nameof(AssociationIndex.FromOrganizationId), column => column.Nullable().WithLength(26))
            .Column<string>(nameof(AssociationIndex.ToPersonId), column => column.Nullable().WithLength(26))
            .Column<string>(nameof(AssociationIndex.ToOrganizationId), column => column.Nullable().WithLength(26))
            .Column<bool>(nameof(AssociationIndex.Published))
            .Column<bool>(nameof(AssociationIndex.Latest)));
        await SchemaBuilder.AlterIndexTableAsync<AssociationIndex>(table => table
            .CreateIndex("IDX_AssociationIndex_From", "DocumentId", "FromPersonId", "Published"));
        await SchemaBuilder.AlterIndexTableAsync<AssociationIndex>(table => table
            .CreateIndex("IDX_AssociationIndex_To", "DocumentId", "ToPersonId", "Published"));
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
            .Column<bool>(nameof(AddressGeoNodeIndex.Published))
            .Column<bool>(nameof(AddressGeoNodeIndex.Latest)));
        await SchemaBuilder.AlterIndexTableAsync<AddressGeoNodeIndex>(table => table
            .CreateIndex("IDX_AddressGeoNodeIndex_Node", "DocumentId", "NodeId", "Published", "Latest"));
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
            // Parties attaches the context reference; Grove never names Person (agents.md).
            .WithPart(RegionsConstants.Parts.RegionalProfileReference, part => part.WithPosition("5"))
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
            .WithPart(RegionsConstants.Parts.RegionalProfileReference, part => part.WithPosition("5"))
            .WithPart(PartiesConstants.Bags.ContactPoints, "BagPart", part => part
                .WithDisplayName("Contact points")
                .WithPosition("2")
                .WithSettings(new BagPartSettings { ContainedContentTypes = [PartiesConstants.ContentTypes.ContactPoint] }))
            .WithPart(PartiesConstants.Bags.Addresses, "BagPart", part => part
                .WithDisplayName("Addresses")
                .WithPosition("3")
                .WithSettings(new BagPartSettings { ContainedContentTypes = [PartiesConstants.ContentTypes.Address] }))
            .WithPart(PartiesConstants.Bags.OrganizationNames, "BagPart", part => part
                .WithDisplayName("Legal entity names")
                .WithPosition("4")
                .WithSettings(new BagPartSettings { ContainedContentTypes = [PartiesConstants.ContentTypes.OrganizationName] })));
    }

    // The party-domain vocabularies this module OWNS (the cross-domain ones - honorifics,
    // languages, countries - stay Grove global lists). DEFERRED, verified the hard way
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
            var contentPartLists = scope.ServiceProvider.GetRequiredService<IContentPartListService>();

            await SeedPartyVocabulariesAsync(contentPartLists);
        });
    }

    private static async Task SeedPartyVocabulariesAsync(IContentPartListService contentPartLists)
    {
        await contentPartLists.SeedAsync(new ContentPartListSeed(
            PartiesOptionSets.ContactPointKind,
            "Contact point kind",
            [
                // Each kind's Category is its type (phone, e-mail, web, social,
                // messaging, other), which the user never selects; reports and the
                // contact-point index group by it (docs/parties.md).
                new(ContactPointKinds.Email, "Email", Category: "email"),
                new(ContactPointKinds.Phone, "Phone", Category: "phone"),
                new(ContactPointKinds.Mobile, "Mobile", Category: "phone"),
                new(ContactPointKinds.Fax, "Fax", Category: "phone"),
                new(ContactPointKinds.Website, "Website", Category: "web"),
                new(ContactPointKinds.Social, "Social", Category: "social"),
                new(ContactPointKinds.Messaging, "Messaging", Category: "messaging"),
                new(ContactPointKinds.Other, "Other", Category: "other"),
            ]));

        await contentPartLists.SeedAsync(new ContentPartListSeed(
            PartiesOptionSets.AddressKind,
            "Address kind",
            [
                new(AddressKinds.Billing, "Billing"),
                new(AddressKinds.Shipping, "Shipping"),
                new(AddressKinds.Mailing, "Mailing"),
                new(AddressKinds.Physical, "Physical"),
                new(AddressKinds.Other, "Other"),
            ]));

        // The legal form only: a trade name is a "dba" legal entity name of the
        // organization, not a kind of organization (docs/parties.md).
        await contentPartLists.SeedAsync(new ContentPartListSeed(
            PartiesOptionSets.OrganizationType,
            "Organization type",
            [
                new("incorporation", "Incorporation"),
                new("llc", "LLC"),
                new("community-group", "Community group"),
                new("ngo", "NGO"),
                new("gov", "Government"),
            ]));

        // Kinds of legal entity name. "dba" is where trade names live (the organization
        // type is the legal form only); a superseded name becomes "former".
        await contentPartLists.SeedAsync(new ContentPartListSeed(
            PartiesOptionSets.OrganizationNameKind,
            "Legal entity name kind",
            [
                new(OrganizationNameKinds.Legal, "Legal name"),
                new(OrganizationNameKinds.Dba, "Trade name (DBA)"),
                new(OrganizationNameKinds.Former, "Former name"),
                new(OrganizationNameKinds.Other, "Other"),
            ]));

        // Association types: the module's own types are seeded and data-locked (code
        // may branch on them); tenants add their own types on top.
        await contentPartLists.SeedAsync(new ContentPartListSeed(
            PartiesOptionSets.AssociationType,
            "Association type",
            [
                new(AssociationTypes.SalesRep, "Sales rep"),
                new(AssociationTypes.AccountRep, "Account rep"),
            ],
            DataLock: true));

        await contentPartLists.AttachToContentTypeAsync(
            PartiesOptionSets.OrganizationType, PartiesConstants.ContentTypes.Organization, "OrganizationType", "Organization type");
        // Industry is NAICS, all levels, held once in the global store (ruling
        // 2026-10-06); tenants add their own industries as additions over it.
        await contentPartLists.AttachToContentTypeAsync(
            GlobalLists.Naics, PartiesConstants.ContentTypes.Organization, "Industry", "Industry");
        await contentPartLists.AttachToContentTypeAsync(
            PartiesOptionSets.AssociationType, PartiesConstants.ContentTypes.Association, "Type", "Type",
            settings => settings.Required = true);
    }

    // The tenant's own organization exists before the first user (staff belong to it):
    // created at tenant setup, after the party types exist, deferred like every content
    // write at first-time setup.
    private static void DeferEnsureTenantOrganization()
    {
        ShellScope.AddDeferredTask(async scope =>
            await scope.ServiceProvider.GetRequiredService<ITenantOrganizationService>().EnsureAsync());
    }

    // Pickers on the bag element types. Registered AFTER the vocabulary seed so the
    // party-owned kind lists exist; the global lists were seeded by Grove's own
    // migration earlier in the deferred queue.
    private static void DeferAttachContactPickers()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var contentPartLists = scope.ServiceProvider.GetRequiredService<IContentPartListService>();

            await contentPartLists.AttachToContentTypeAsync(
                PartiesOptionSets.ContactPointKind, PartiesConstants.ContentTypes.ContactPoint, "Kind", "Kind",
                settings => settings.Required = true);
            await contentPartLists.AttachToContentTypeAsync(
                GlobalPhoneCountryCodes, PartiesConstants.ContentTypes.ContactPoint, "PhoneCountry", "Phone country");

            await contentPartLists.AttachToContentTypeAsync(
                PartiesOptionSets.AddressKind, PartiesConstants.ContentTypes.Address, "Kind", "Kind",
                settings => settings.Required = true);
            await contentPartLists.AttachToContentTypeAsync(
                PartiesOptionSets.OrganizationNameKind, PartiesConstants.ContentTypes.OrganizationName, "Kind", "Kind",
                settings => settings.Required = true);
        });
    }

    // Tenants provisioned before the global reference lists were wired in.
    // Person's name furniture and language come from Grove's shared GLOBAL lists
    // (seeded by the Grove.Contents.PartLists feature itself) rather than lists this
    // module owns - honorifics, name suffixes and languages cross every domain
    // boundary.
    //
    // DEFERRED, because AttachToContentTypeAsync resolves the target list and the
    // global lists are themselves seeded in a deferred task (content creation during
    // first-time setup cannot run inline - see docs/global-store.md).
    // Deferred tasks run in registration order and feature dependencies put Grove's
    // migration first, so the lists exist by the time this attaches to them.
    private static void DeferAttachGlobalListPickers()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var contentPartLists = scope.ServiceProvider.GetRequiredService<IContentPartListService>();

            await contentPartLists.AttachToContentTypeAsync(GlobalLists.Honorifics, PartiesConstants.ContentTypes.Person, "Honorific", "Honorific");
            await contentPartLists.AttachToContentTypeAsync(GlobalLists.NameSuffixes, PartiesConstants.ContentTypes.Person, "Suffix", "Suffix");
            await contentPartLists.AttachToContentTypeAsync(GlobalLists.Languages, PartiesConstants.ContentTypes.Person, "PreferredLanguage", "Preferred language");
        });
    }
}
