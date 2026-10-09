using Crest.Services;
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
        services.AddScoped<ICrestRoutePermissionProvider, PartiesRoutePermissionProvider>();
        services.AddScoped<PartyTypeCatalog>();
        services.AddScoped<IPartyTypeProvider, BasePartyTypeProvider>();
        // Registered with the workflow registry: role created/removed, raised from the content handler.
        services.AddScoped<Crest.Workflows.IWorkflowTriggerProvider, Workflows.PartiesWorkflowProvider>();
        services.AddScoped<Crest.ContentManagement.Handlers.IContentHandler, Workflows.PartyRoleWorkflowHandler>();
        services.AddScoped<Crest.Workflows.IWorkflowActivityProvider, Workflows.PartiesWorkflowProvider>();
        services.ConfigureCrestWorkflows(workflows => workflows.AddActivitiesFrom<Startup>());

        services.AddScoped<PartyOptionKeys>();
        services.AddScoped<PartyContactsService>();
        services.AddScoped<IPartyContactsService>(provider => provider.GetRequiredService<PartyContactsService>());
        services.AddScoped<IPartyUserLinkService, PartyUserLinkService>();
        services.AddScoped<IPartyPositionsService, PartyPositionsService>();
        services.AddScoped<PartyMapper>();

        services.AddIndexProvider<PartyPositionIndexProvider>();
        services.AddIndexProvider<AddressGeoNodeIndexProvider>();
        services.AddIndexProvider<LocationIndexProvider>();
        services.AddScoped<ILocationService, LocationService>();
    }
}
