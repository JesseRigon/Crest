using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.AuditTrail.Models;
using Grove.AuditTrail.Services;
using Grove.AuditTrail.Services.Models;
using Grove.ContentManagement;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentManagement.Handlers;
using Grove.Contents.AuditTrail.Drivers;
using Grove.Contents.AuditTrail.Handlers;
using Grove.Contents.AuditTrail.Models;
using Grove.Contents.AuditTrail.Services;
using Grove.ContentTypes.Editors;
using Grove.Data;
using Grove.Data.Migration;
using Grove.DisplayManagement.Handlers;
using Grove.Modules;

namespace Grove.Contents.AuditTrail;

[RequireFeatures("Grove.AuditTrail")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<Migrations>();
        services.AddContentPart<AuditTrailPart>()
            .UseDisplayDriver<AuditTrailPartDisplayDriver>();

        services.AddScoped<IContentDisplayDriver, AuditTrailContentsDriver>();

        services.AddTransient<IConfigureOptions<AuditTrailOptions>, ContentAuditTrailEventConfiguration>();
        services.AddScoped<ContentHistory>();
        services.AddScoped<IAuditTrailEventHandler, ContentAuditTrailEventHandler>();
        services.AddIndexProvider<Indexes.ContentHistoryIndexProvider>();
        services.AddSiteDisplayDriver<ContentAuditTrailSettingsDisplayDriver>();

        services.AddDisplayDriver<AuditTrailEvent, AuditTrailContentEventDisplayDriver>();

        services.AddScoped<IContentHandler, AuditTrailContentHandler>();
    }
}
