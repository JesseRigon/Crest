using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Crest.AuditTrail.Models;
using Crest.AuditTrail.Services;
using Crest.AuditTrail.Services.Models;
using Crest.ContentManagement;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Handlers;
using Crest.Contents.AuditTrail.Drivers;
using Crest.Contents.AuditTrail.Handlers;
using Crest.Contents.AuditTrail.Models;
using Crest.Contents.AuditTrail.Services;
using Crest.ContentTypes.Editors;
using Crest.Data.Migration;
using Crest.DisplayManagement.Handlers;
using Crest.Modules;

namespace Crest.Contents.AuditTrail;

[RequireFeatures("Crest.AuditTrail")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<Migrations>();
        services.AddContentPart<AuditTrailPart>()
            .UseDisplayDriver<AuditTrailPartDisplayDriver>();

        services.AddScoped<IContentTypePartDefinitionDisplayDriver, AuditTrailPartSettingsDisplayDriver>();
        services.AddScoped<IContentDisplayDriver, AuditTrailContentsDriver>();

        services.AddTransient<IConfigureOptions<AuditTrailOptions>, ContentAuditTrailEventConfiguration>();
        services.AddScoped<IAuditTrailEventHandler, ContentAuditTrailEventHandler>();
        services.AddSiteDisplayDriver<ContentAuditTrailSettingsDisplayDriver>();

        services.AddDisplayDriver<AuditTrailEvent, AuditTrailContentEventDisplayDriver>();

        services.AddScoped<IContentHandler, AuditTrailContentHandler>();
    }
}
