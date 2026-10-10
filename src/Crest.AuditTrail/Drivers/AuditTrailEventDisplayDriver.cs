using Crest.AuditTrail.Models;
using Crest.AuditTrail.Services;
using Crest.AuditTrail.Services.Models;
using Crest.AuditTrail.ViewModels;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;

namespace Crest.AuditTrail.Drivers;

public sealed class AuditTrailEventDisplayDriver : DisplayDriver<AuditTrailEvent>
{
    private readonly IAuditTrailManager _auditTrailManager;

    public AuditTrailEventDisplayDriver(IAuditTrailManager auditTrailManager)
    {
        _auditTrailManager = auditTrailManager;
    }

    public override Task<IDisplayResult> DisplayAsync(AuditTrailEvent auditTrailEvent, BuildDisplayContext context)
    {
        var descriptor = _auditTrailManager.DescribeEvent(auditTrailEvent);

        return CombineAsync(
            Initialize<AuditTrailEventViewModel>("AuditTrailEventTags_SummaryAdmin", model => BuildViewModel(auditTrailEvent, model, descriptor))
                .Location(PlatformConstants.DisplayType.SummaryAdmin, "EventTags:10"),
            Initialize<AuditTrailEventViewModel>("AuditTrailEventMeta_SummaryAdmin", model => BuildViewModel(auditTrailEvent, model, descriptor))
                .Location(PlatformConstants.DisplayType.SummaryAdmin, "EventMeta:10"),
            Initialize<AuditTrailEventViewModel>("AuditTrailEventActions_SummaryAdmin", model => BuildViewModel(auditTrailEvent, model, descriptor))
                .Location(PlatformConstants.DisplayType.SummaryAdmin, "Actions:10"),
            Initialize<AuditTrailEventViewModel>("AuditTrailEventDetail_DetailAdmin", model => BuildViewModel(auditTrailEvent, model, descriptor))
                .Location(PlatformConstants.DisplayType.DetailAdmin, "Content:before")
        );
    }

    public static void BuildViewModel(AuditTrailEvent auditTrailEvent, AuditTrailEventViewModel model, AuditTrailEventDescriptor descriptor)
    {
        model.AuditTrailEvent = auditTrailEvent;
        model.Descriptor = descriptor;
    }
}
