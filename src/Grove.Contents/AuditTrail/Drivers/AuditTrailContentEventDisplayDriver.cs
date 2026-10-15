using System.Text.Json.Nodes;
using Grove.AuditTrail.Drivers;
using Grove.AuditTrail.Indexes;
using Grove.AuditTrail.Models;
using Grove.AuditTrail.Services;
using Grove.AuditTrail.Services.Models;
using Grove.ContentManagement.Records;
using Grove.Contents.AuditTrail.Models;
using Grove.Contents.AuditTrail.Services;
using Grove.Contents.AuditTrail.ViewModels;
using Grove.DisplayManagement.Handlers;
using Grove.DisplayManagement.Views;
using Grove.Entities;
using YesSql;

namespace Grove.Contents.AuditTrail.Drivers;

public sealed class AuditTrailContentEventDisplayDriver : AuditTrailEventSectionDisplayDriver<AuditTrailContentEvent>
{
    private readonly Dictionary<string, string> _latestVersionId = [];
    private readonly IAuditTrailManager _auditTrailManager;
    private readonly ISession _session;
    private readonly ContentHistory _history;

    public AuditTrailContentEventDisplayDriver(
        IAuditTrailManager auditTrailManager,
        ISession session,
        ContentHistory history)
    {
        _auditTrailManager = auditTrailManager;
        _session = session;
        _history = history;
    }

    public override async Task<IDisplayResult> DisplayAsync(AuditTrailEvent auditTrailEvent, AuditTrailContentEvent contentEvent, BuildDisplayContext context)
    {
        // Lists show the summary the event carries; the detail rebuilds the versions it compares
        // (docs/audit.md › Content history is stored as diffs).
        var summaryItem = contentEvent.Stub();
        var contentItemId = summaryItem.ContentItemId;

        if (!_latestVersionId.TryGetValue(contentItemId, out var latestVersionId))
        {
            latestVersionId = (await _session.QueryIndex<ContentItemIndex>(index => index.ContentItemId == contentItemId && index.Latest)
                .FirstOrDefaultAsync())
                ?.ContentItemVersionId;

            _latestVersionId[contentItemId] = latestVersionId;
        }

        var descriptor = _auditTrailManager.DescribeEvent(auditTrailEvent);

        async ValueTask BuildDetailAsync(AuditTrailContentEventDetailViewModel m)
        {
            var current = await _history.RebuildAsync(auditTrailEvent) ?? summaryItem;
            BuildSummaryViewModel(m, auditTrailEvent, contentEvent, descriptor, latestVersionId, current);

            var previousContentItem = await _history.RebuildPreviousAsync(auditTrailEvent);
            if (previousContentItem != null)
            {
                var currentJson = JObject.FromObject(current);
                var previousJson = JObject.FromObject(previousContentItem);
                previousJson.Remove(nameof(AuditTrailPart));
                currentJson.Remove(nameof(AuditTrailPart));

                m.PreviousContentItem = previousContentItem;
                m.Previous = previousJson.ToString();
                m.Current = currentJson.ToString();
            }
        }

        return Combine(
            Initialize<AuditTrailContentEventViewModel>("AuditTrailContentEventData_SummaryAdmin", m => BuildSummaryViewModel(m, auditTrailEvent, contentEvent, descriptor, latestVersionId, summaryItem))
                .Location(PlatformConstants.DisplayType.SummaryAdmin, "EventData:10"),
            Initialize<AuditTrailContentEventViewModel>("AuditTrailContentEventContent_SummaryAdmin", m => BuildSummaryViewModel(m, auditTrailEvent, contentEvent, descriptor, latestVersionId, summaryItem))
                .Location(PlatformConstants.DisplayType.SummaryAdmin, "Content:10"),
            Initialize<AuditTrailContentEventViewModel>("AuditTrailContentEventActions_SummaryAdmin", m => BuildSummaryViewModel(m, auditTrailEvent, contentEvent, descriptor, latestVersionId, summaryItem))
                .Location(PlatformConstants.DisplayType.SummaryAdmin, "Actions:5"),
            Initialize<AuditTrailContentEventDetailViewModel>("AuditTrailContentEventDetail_DetailAdmin", BuildDetailAsync)
                .Location(PlatformConstants.DisplayType.DetailAdmin, "Content:5"),
            Initialize<AuditTrailContentEventDetailViewModel>("AuditTrailContentEventDiff_DetailAdmin", BuildDetailAsync)
                .Location(PlatformConstants.DisplayType.DetailAdmin, "Content:5#Diff")
        );
    }

    private static void BuildSummaryViewModel(AuditTrailContentEventViewModel m, AuditTrailEvent model, AuditTrailContentEvent contentEvent, AuditTrailEventDescriptor descriptor, string latestVersionId, Grove.ContentManagement.ContentItem contentItem)
    {
        m.AuditTrailEvent = model;
        m.Descriptor = descriptor;
        m.Name = contentEvent.Name;
        m.ContentItem = contentItem;
        m.VersionNumber = contentEvent.VersionNumber;
        m.Comment = contentEvent.Comment;
        m.LatestVersionId = latestVersionId;
        m.ContentEvent = contentEvent;
    }
}
