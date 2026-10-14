using Crest.ContentManagement;
using Crest.Services;
using Crest.ContentFields.Relationships;
using Crest.Parties.Constants;
using Crest.Workflows.Extensions;
using Crest.Parties.Indexes;
using Crest.Parties.Migrations;
using Crest.Parties.Navigation;
using Crest.Parties.PartyTypes;
using Crest.Parties.Permissions;
using Crest.Parties.Services;
using Crest.Parties.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Crest.Data;
using Crest.Data.Migration;
using Crest.Modules;
using Crest.Navigation;

namespace Crest.Parties;

[Feature("Crest.Parties")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<PartiesMigrations>();
        services.AddNavigationProvider<Navigation.AdminMenu>();
        services.AddScoped<Crest.ContentGroups.IContentGroupProvider, PartiesContentGroupProvider>();
        services.AddScoped<Crest.Access.IRoutePermissionProvider, PartiesRoutePermissionProvider>();
        services.AddScoped<PartyTypeCatalog>();
        services.AddScoped<IPartyTypeProvider, BasePartyTypeProvider>();
        // Registered with the workflow registry: role created/removed, raised from the content handler.
        services.AddScoped<Crest.Workflows.IWorkflowTriggerProvider, Workflows.PartiesWorkflowProvider>();
        services.AddScoped<Crest.ContentManagement.Handlers.IContentHandler, Workflows.PartyRoleWorkflowHandler>();
        services.AddScoped<Crest.Workflows.IWorkflowActivityProvider, Workflows.PartiesWorkflowProvider>();
        services.ConfigureCrestWorkflows(workflows => workflows.AddActivitiesFrom<Startup>());

        services.AddScoped<PartyOptionKeys>();
        services.AddScoped<PartyBagElementService>();
        services.AddScoped<IPartyNamesService, PartyNamesService>();
        services.AddScoped<ITenantOrganizationService, TenantOrganizationService>();
        services.AddScoped<IPartyAssociationService, PartyAssociationService>();
        services.AddScoped<IIndividualOrganizationService, IndividualOrganizationService>();
        services.AddScoped<IPartyRoleService, PartyRoleService>();
        // The parent picker's cycle rule, on every save path (docs/content-items.md ›
        // Picker relationship handlers).
        services.AddContentPickerRelationshipHandler<ParentOrganizationRelationshipHandler>(
            PartiesConstants.ContentTypes.Organization, PartiesConstants.OrganizationFields.ParentOrganization);
        services.AddScoped<PartyContactsService>();
        services.AddScoped<IPartyContactsService>(provider => provider.GetRequiredService<PartyContactsService>());
        services.AddScoped<IPartyUserLinkService, PartyUserLinkService>();
        services.AddScoped<IPartyPositionsService, PartyPositionsService>();
        // A person's own record and an organization's contacts (docs/access.md § Members).
        services.AddScoped<Crest.Access.IResourcePermissionMapper, PartyPermissionMapper>();
        services.AddScoped<PartyMapper>();

        services.AddIndexProvider<PartyPositionIndexProvider>();
        services.AddIndexProvider<AddressGeoNodeIndexProvider>();
        services.AddIndexProvider<LocationIndexProvider>();
        services.AddIndexProvider<ContactPointIndexProvider>();
        services.AddIndexProvider<OrganizationNameIndexProvider>();
        services.AddIndexProvider<AssociationIndexProvider>();
        services.AddScoped<ILocationService, LocationService>();

        // Parties' reads go through the scope: a Location's or a position's row is readable
        // when its content item is (docs/access.md, Domains onto the extension system).
        services.AddScoped<Crest.Access.IScopeProvider>(provider => new PartyScopeProvider(nameof(LocationIndex), provider));
        services.AddScoped<Crest.Access.IScopeProvider>(provider => new PartyScopeProvider(nameof(PartyPositionIndex), provider));

        // Organization ownership on the row (ruled 2026-10-14, moved here from Crest.Server and
        // Crest.Members): the owner part and index, the deployment-time registry of
        // organization-scoped types, and the organization row scope.
        services.AddSingleton<Organizations.OrganizationScopedTypes>();
        services.AddIndexProvider<Organizations.OrganizationIndexProvider>();
        services.AddDataMigration<Organizations.CrestOrganizationIndexMigrations>();
        services.AddContentPart<Organizations.CrestOrganizationPart>();
        services.AddScoped<Crest.Access.IScopeProvider, Organizations.OrganizationScopeProvider>();
    }
}
