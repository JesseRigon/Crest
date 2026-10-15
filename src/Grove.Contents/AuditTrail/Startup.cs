using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.AuditTrail.Models;
using Grove.AuditTrail.Services;
using Grove.AuditTrail.Services.Models;
using Grove.Contents.Management;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Contents.Management.Handlers;
using Grove.Contents.AuditTrail.Handlers;
using Grove.Contents.AuditTrail.Models;
using Grove.Contents.AuditTrail.Services;
using Grove.Data;
using Grove.Data.Migration;
using Grove.Display.Management.Handlers;
using Grove.Modules;

namespace Grove.Contents.AuditTrail;

[RequireFeatures("Grove.AuditTrail")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<Migrations>();
        // The version comment is edited on the editor tree (TextFieldEditor edits AuditTrailPart).
        services.AddContentPart<AuditTrailPart>();


        services.AddTransient<IConfigureOptions<AuditTrailOptions>, ContentAuditTrailEventConfiguration>();
        services.AddScoped<ContentHistory>();
        services.AddScoped<IAuditTrailEventHandler, ContentAuditTrailEventHandler>();
        services.AddIndexProvider<Indexes.ContentHistoryIndexProvider>();


        services.AddScoped<IContentHandler, AuditTrailContentHandler>();
        services.AddApiContributor<Api.ContentAuditTrailSettingsApi>();
        services.AddApiContributor<Api.AuditTrailContentApi>();
    }
}
