using Elsa.Common.Multitenancy.HostedServices;
using Elsa.Extensions;
using Elsa.Mediator.HostedServices;
using Elsa.Mediator.Options;
using Elsa.Resilience.Extensions;
using Elsa.Workflows.Runtime.Distributed.Extensions;
using Medallion.Threading.FileSystem;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.Crest.Workflows.Contexts;
using OrchardCore.Crest.Workflows.Handlers.Content;
using OrchardCore.Crest.Workflows.Indexes;
using OrchardCore.Crest.Workflows.Migrations;
using OrchardCore.Crest.Workflows.Security;
using OrchardCore.Crest.Workflows.Services;
using OrchardCore.Crest.Workflows.StartupTasks;
using OrchardCore.Crest.Workflows.Stores;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace OrchardCore.Crest.Workflows.Features;

[Feature("OrchardCore.Crest.Workflows")]
public class CoreStartup(IOptions<ShellOptions> shellOptions, ShellSettings shellSettings) : StartupBase
{
    // Last of all startups: Orchard orders them by feature dependency and then stably by
    // Order, so this is where the stock OrchardCore.Workflows services get overridden
    // (plans/workflows.md, "Override").
    public override int Order => int.MaxValue;

    // The pipeline is a different order. With endpoint routing, the authorization
    // middleware (Users module, default order) evaluates the matched Elsa endpoint's policy
    // when it runs, so the Elsa grant must already be on the principal by then. This
    // startup's middleware is therefore slotted ahead of the authentication order; the
    // Elsa branch authenticates the request itself (see ConfigureAsync) so the gate sees
    // the real principal, then the grant is in place before UseAuthorization.
    public override int ConfigureOrder => OrchardCoreConstants.ConfigureOrder.Authentication - 1;

    public override void ConfigureServices(IServiceCollection services)
    {
        // One Elsa per shell. Every Elsa service, store and hosted service below lives in
        // the tenant's container; the stores use the tenant's ISession. The file lock
        // directory is the one thing Elsa would otherwise share across tenants (its
        // default is App_Data/locks relative to the process): put it under this shell's
        // own App_Data so tenants, and the dev/test instances, never lock on each other.
        var lockDirectory = Path.Combine(
            shellOptions.Value.ShellsApplicationDataPath,
            shellOptions.Value.ShellsContainerName,
            shellSettings.Name,
            "locks");

        services.AddElsa(elsa =>
        {
            elsa.AddActivitiesFrom<CoreStartup>();
            elsa.UseWorkflowManagement(workflowManagement =>
            {
                workflowManagement.UseWorkflowDefinitionPublisher(sp => ActivatorUtilities.CreateInstance<ContentItemWorkflowDefinitionPublisher>(sp));
                workflowManagement.UseWorkflowDefinitions(workflowDefinitions => workflowDefinitions.WorkflowDefinitionStore = sp => ActivatorUtilities.CreateInstance<ElsaWorkflowDefinitionStore>(sp));
                workflowManagement.UseWorkflowInstances(workflowInstances => workflowInstances.WorkflowInstanceStore = sp => ActivatorUtilities.CreateInstance<ElsaWorkflowInstanceStore>(sp));
                workflowManagement.UseCache();
            });
            elsa.UseWorkflowRuntime(workflowRuntime =>
            {
                workflowRuntime.TriggerStore = sp => ActivatorUtilities.CreateInstance<ElsaTriggerStore>(sp);
                workflowRuntime.BookmarkStore = sp => ActivatorUtilities.CreateInstance<ElsaBookmarkStore>(sp);
                workflowRuntime.WorkflowExecutionLogStore = sp => ActivatorUtilities.CreateInstance<ElsaWorkflowExecutionLogStore>(sp);
                workflowRuntime.ActivityExecutionLogStore = sp => ActivatorUtilities.CreateInstance<ElsaActivityExecutionRecordStore>(sp);
                workflowRuntime.DistributedLockProvider = _ =>
                {
                    Directory.CreateDirectory(lockDirectory);
                    return new FileDistributedSynchronizationProvider(new DirectoryInfo(lockDirectory));
                };
                workflowRuntime.UseDistributedRuntime();
                workflowRuntime.UseCache();
            });
            elsa.UseJavaScript();
            elsa.UseLiquid();
            elsa.UseResilience();
            elsa.UseWorkflowsApi(api => api.AddFastEndpointsAssembly<CoreStartup>());
        });

        // Orchard may dispose the tenant container synchronously; Elsa's tenant service is
        // async-only and would crash the process on that path (SyncDisposableTenantService).
        SyncDisposableTenantService.ReplaceIn(services);

        // The sample host set this at the process level; here it is per shell.
        services.Configure<MediatorOptions>(options => options.JobWorkerCount = 1);

        services.Configure<StoreCollectionOptions>(o =>
        {
            o.Collections.Add(ElsaCollections.WorkflowInstances);
            o.Collections.Add(ElsaCollections.StoredTriggers);
            o.Collections.Add(ElsaCollections.StoredBookmarks);
            o.Collections.Add(ElsaCollections.WorkflowExecutionLogRecords);
            o.Collections.Add(ElsaCollections.ActivityExecutionRecords);
        });

        services
            .AddDataMigration<WorkflowDefinitionMigrations>()
            .AddDataMigration<WorkflowInstanceMigrations>()
            .AddDataMigration<StoredTriggerMigrations>()
            .AddDataMigration<StoredBookmarkMigrations>()
            .AddDataMigration<WorkflowExecutionLogRecordMigrations>()
            .AddDataMigration<ActivityExecutionRecordMigrations>()
            .AddSingleton<JobRunnerHostedService>()
            .AddSingleton<ActivateTenants>()
            .AddScoped<IModularTenantEvents, PopulateRegistriesTask>()
            .AddScoped<IModularTenantEvents, StartHostedServices>()
            .AddScoped<IContentHandler, WorkflowDefinitionContentHandler>()
            .AddScoped<WorkflowDefinitionPartMapper>()
            .AddScoped<WorkflowDefinitionPartSerializer>()
            .AddScoped<IWorkflowUserContextAccessor, WorkflowUserContextAccessor>()
            .AddScoped<IWorkflowAuthorizer, WorkflowAuthorizer>()
            .AddIndexProvider<WorkflowDefinitionIndexProvider>()
            .AddIndexProvider<WorkflowInstanceIndexProvider>()
            .AddIndexProvider<StoredTriggerIndexProvider>()
            .AddIndexProvider<StoredBookmarkIndexProvider>()
            .AddIndexProvider<WorkflowExecutionLogRecordIndexProvider>()
            .AddIndexProvider<ActivityExecutionRecordIndexProvider>();
    }

    public override ValueTask ConfigureAsync(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        // Placed before the Users module's UseAuthentication/UseAuthorization pair (see
        // ConfigureOrder). The gate authenticates the request itself for the Elsa path so
        // it sees the real principal, then adds the Elsa grant before the authorization
        // middleware evaluates the endpoint's policy. Running the cookie scheme twice on a
        // request is harmless: same scheme, same ticket, no second sign-in.
        app.UseWhen(
            context => context.Request.Path.StartsWithSegments(ElsaApiSecurityMiddleware.ApiPathPrefix),
            branch =>
            {
                branch.UseAuthentication();
                branch.UseMiddleware<ElsaApiSecurityMiddleware>();
            });
        routes.MapWorkflowsApi();
        return ValueTask.CompletedTask;
    }
}
