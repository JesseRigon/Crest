using Grove.Contents.Management;
using Grove.Services;
using Grove.Contents.Fields.Relationships;
using Grove.Parties.Constants;
using Grove.Workflows.Extensions;
using Grove.Parties.Indexes;
using Grove.Parties.Migrations;
using Grove.Parties.Navigation;
using Grove.Parties.PartyTypes;
using Grove.Parties.Services;
using Grove.Parties.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Grove.Data;
using Grove.Data.Migration;
using Grove.Api;
using Grove.Modules;
using Grove.Display.Navigation;

namespace Grove.Parties;

[Feature("Grove.Parties")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<PartiesMigrations>();
        services.AddPageSource<PartiesPages>();
        services.AddScoped<Grove.ContentGroups.IContentGroupProvider, PartiesContentGroupProvider>();
        services.AddScoped<PartyTypeCatalog>();
        services.AddScoped<IPartyTypeProvider, BasePartyTypeProvider>();
        // Registered with the workflow registry: role created/removed, raised from the content handler.
        services.AddScoped<Grove.Workflows.IWorkflowTriggerProvider, Workflows.PartiesWorkflowProvider>();
        services.AddScoped<Grove.Contents.Management.Handlers.IContentHandler, Workflows.PartyRoleWorkflowHandler>();
        services.AddScoped<Grove.Workflows.IWorkflowActivityProvider, Workflows.PartiesWorkflowProvider>();
        services.ConfigureGroveWorkflows(workflows => workflows.AddActivitiesFrom<Startup>());

        services.AddScoped<PartyOptionKeys>();
        services.AddScoped<PartyBagElementService>();
        services.AddScoped<IPartyNamesService, PartyNamesService>();
        services.AddScoped<ITenantOrganizationService, TenantOrganizationService>();
        services.AddScoped<IPartyAssociationService, PartyAssociationService>();
        services.AddScoped<IIndividualOrganizationService, IndividualOrganizationService>();
        services.AddScoped<IPartyRoleService, PartyRoleService>();
        // The parent picker's cycle rule, on every save path (docs/content-engine.md ›
        // Relationship guard).
        services.AddContentPickerRelationshipHandler<ParentOrganizationRelationshipHandler>(
            PartiesConstants.ContentTypes.Organization, PartiesConstants.OrganizationFields.ParentOrganization);
        services.AddScoped<PartyContactsService>();
        services.AddScoped<IPartyContactsService>(provider => provider.GetRequiredService<PartyContactsService>());
        services.AddScoped<IPartyUserLinkService, PartyUserLinkService>();
        services.AddScoped<IPartyPositionsService, PartyPositionsService>();
        // A person's own record and an organization's contacts (docs/access.md § Members).
        services.AddScoped<Grove.Access.IResourcePermissionMapper, PartyPermissionMapper>();
        services.AddScoped<PartyMapper>();

        services.AddApiContributor<Api.PartyAssociationsApi>();
        services.AddApiContributor<Api.PartyContactsApi>();
        services.AddApiContributor<Api.PartyNamesApi>();
        services.AddApiContributor<Api.PartyPositionsApi>();
        services.AddApiContributor<Api.PartyRolesApi>();
        services.AddApiContributor<Api.PartyTypesApi>();

        services.AddIndexProvider<PartyPositionIndexProvider>();
        services.AddIndexProvider<AddressGeoNodeIndexProvider>();
        services.AddIndexProvider<LocationIndexProvider>();
        services.AddIndexProvider<ContactPointIndexProvider>();
        services.AddIndexProvider<OrganizationNameIndexProvider>();
        services.AddIndexProvider<AssociationIndexProvider>();
        services.AddScoped<ILocationService, LocationService>();

        // Parties' reads go through the scope: a Location's or a position's row is readable
        // when its content item is (docs/access.md, Domains onto the extension system).
        services.AddScoped<Grove.Access.IScopeProvider>(provider => new PartyScopeProvider(nameof(LocationIndex), provider));
        services.AddScoped<Grove.Access.IScopeProvider>(provider => new PartyScopeProvider(nameof(PartyPositionIndex), provider));

        // Organization ownership on the row (ruled 2026-10-14, moved here from Grove.Infrastructure.Server and
        // Grove.Access.Members): the owner part and index, the deployment-time registry of
        // organization-scoped types, and the organization row scope.
        services.AddSingleton<Organizations.OrganizationScopedTypes>();
        services.AddIndexProvider<Organizations.OrganizationIndexProvider>();
        services.AddDataMigration<Organizations.GroveOrganizationIndexMigrations>();
        services.AddContentPart<Organizations.GroveOrganizationPart>();
        services.AddScoped<Grove.Access.IScopeProvider, Organizations.OrganizationScopeProvider>();
    }
}
