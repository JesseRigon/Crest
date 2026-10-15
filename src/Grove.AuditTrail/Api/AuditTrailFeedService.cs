#nullable enable

using System.Text.Json.Nodes;
using Grove.AuditTrail.Indexes;
using Grove.AuditTrail.Models;
using Grove.AuditTrail.Services;
using Grove.Entities;
using Microsoft.AspNetCore.Identity;
using YesSql;
using YesSql.Services;

namespace Grove.AuditTrail.Api;

/// <summary>
/// Reads the trail for the audit pages (docs/audit.md › step 4). Every filter goes into the
/// query over the attribution index, before paging; who may call it at all is the
/// controller's decision (the audit permission). Activity feeds at volume (keyset paging, the
/// caller's scope) are a separate step (A4).
/// </summary>
public sealed class AuditTrailFeedService(
    ISession session,
    IAuditTrailManager auditTrailManager,
    ILookupNormalizer normalizer,
    IServiceProvider services)
{
    public const int MaxTake = 200;

    public async Task<AuditTrailEventPage> QueryAsync(AuditTrailEventFilter filter)
    {
        var take = Math.Clamp(filter.Take, 1, MaxTake);
        var skip = Math.Max(0, filter.Skip);

        // Each index is joined once: the optional ones first, the attribution index last, so
        // ordering and the main filters run on it.
        IQuery<AuditTrailEvent> root = session.Query<AuditTrailEvent>(collection: AuditTrailEvent.Collection);

        if (!string.IsNullOrWhiteSpace(filter.UserName))
        {
            var normalized = normalizer.NormalizeName(filter.UserName.Trim());
            root = root.With<AuditTrailEventIndex>(index => index.NormalizedUserName == normalized);
        }

        if (!string.IsNullOrWhiteSpace(filter.PartyId))
        {
            var partyId = filter.PartyId.Trim();
            root = root.With<AuditTrailPartyIndex>(index => index.PartyId == partyId);
        }

        var query = Filter(root.With<AuditTrailAttributionIndex>(), filter);

        var total = await query.CountAsync();
        var events = await query.OrderByDescending(index => index.CreatedUtc).ThenByDescending(index => index.Id).Skip(skip).Take(take).ListAsync();

        return new AuditTrailEventPage(events.Select(e => ToModel(e, includeData: false)).ToArray(), total, skip, take);
    }

    public async Task<AuditTrailEventModel?> GetAsync(string eventId)
    {
        var auditTrailEvent = await auditTrailManager.GetEventAsync(eventId);
        return auditTrailEvent is null ? null : ToModel(auditTrailEvent, includeData: true);
    }

    /// <summary>Every event for one record, newest first (a record's history tab).</summary>
    public Task<AuditTrailEventPage> RecordHistoryAsync(string recordId, int skip, int take)
        => QueryAsync(new AuditTrailEventFilter { RecordId = recordId, Skip = skip, Take = take });

    /// <summary>
    /// Impersonation sessions, newest first: each start event, with the session's stop and the
    /// number of events recorded inside it (grouped through the session id).
    /// </summary>
    public async Task<AuditTrailSessionPage> SessionsAsync(AuditTrailEventFilter filter)
    {
        var take = Math.Clamp(filter.Take, 1, MaxTake);
        var skip = Math.Max(0, filter.Skip);

        var starts = session.QueryIndex<AuditTrailAttributionIndex>(index =>
            index.Category == AuditTrailSessionEvents.Category && index.Name == AuditTrailSessionEvents.Started,
            collection: AuditTrailEvent.Collection);

        if (!string.IsNullOrWhiteSpace(filter.UserId))
        {
            var userId = filter.UserId.Trim();
            starts = starts.Where(index => index.UserId == userId);
        }

        if (!string.IsNullOrWhiteSpace(filter.ImpersonatedUserId))
        {
            var memberId = filter.ImpersonatedUserId.Trim();
            starts = starts.Where(index => index.ImpersonatedUserId == memberId);
        }

        if (!string.IsNullOrWhiteSpace(filter.OrganizationId))
        {
            var organizationId = filter.OrganizationId.Trim();
            starts = starts.Where(index => index.OrganizationId == organizationId);
        }

        if (filter.From is { } from)
        {
            starts = starts.Where(index => index.CreatedUtc >= from);
        }

        if (filter.To is { } to)
        {
            starts = starts.Where(index => index.CreatedUtc <= to);
        }

        var total = await starts.CountAsync();
        var page = (await starts.OrderByDescending(index => index.CreatedUtc).Skip(skip).Take(take).ListAsync()).ToArray();

        var sessionIds = page.Select(start => start.SessionId).Where(id => id is not null).Distinct().ToArray();
        var members = sessionIds.Length == 0
            ? []
            : (await session.QueryIndex<AuditTrailAttributionIndex>(index => index.SessionId.IsIn(sessionIds), collection: AuditTrailEvent.Collection).ListAsync()).ToArray();

        var startEvents = new Dictionary<string, AuditTrailEvent>(StringComparer.Ordinal);
        foreach (var start in page)
        {
            if (await auditTrailManager.GetEventAsync(start.EventId) is { } startEvent)
            {
                startEvents[start.EventId] = startEvent;
            }
        }

        var items = page.Select(start =>
        {
            var rows = members.Where(row => row.SessionId == start.SessionId).ToArray();
            var stop = rows.Where(row => row.Category == AuditTrailSessionEvents.Category && row.Name == AuditTrailSessionEvents.Stopped)
                .OrderBy(row => row.CreatedUtc)
                .FirstOrDefault();
            startEvents.TryGetValue(start.EventId, out var startEvent);
            var attribution = startEvent?.As<AuditTrailAttribution>();

            return new AuditTrailSessionModel(
                start.SessionId ?? start.EventId,
                start.UserId,
                startEvent?.UserName,
                start.ImpersonatedUserId,
                attribution?.ImpersonatedUserName,
                start.OrganizationId,
                start.CreatedUtc,
                stop?.CreatedUtc,
                rows.Count(row => row.Category != AuditTrailSessionEvents.Category));
        }).ToArray();

        return new AuditTrailSessionPage(items, total, skip, take);
    }

    public IReadOnlyList<AuditTrailCategoryModel> Categories() => auditTrailManager.DescribeCategories()
        .Select(category => new AuditTrailCategoryModel(
            category.Name,
            category.LocalizedName(services).Value,
            category.Events.Values.Select(e => new AuditTrailEventNameModel(e.Name, e.LocalizedName(services).Value)).ToArray()))
        .OrderBy(category => category.Text, StringComparer.CurrentCultureIgnoreCase)
        .ToArray();

    internal static IQuery<AuditTrailEvent, AuditTrailAttributionIndex> Filter(IQuery<AuditTrailEvent, AuditTrailAttributionIndex> query, AuditTrailEventFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.UserId))
        {
            var userId = filter.UserId.Trim();
            query = query.Where(index => index.UserId == userId);
        }

        if (!string.IsNullOrWhiteSpace(filter.FunctionalUserId))
        {
            var functionalUserId = filter.FunctionalUserId.Trim();
            query = query.Where(index => index.FunctionalUserId == functionalUserId);
        }

        if (!string.IsNullOrWhiteSpace(filter.ImpersonatedUserId))
        {
            var impersonatedUserId = filter.ImpersonatedUserId.Trim();
            query = query.Where(index => index.ImpersonatedUserId == impersonatedUserId);
        }

        if (filter.ImpersonatedOnly)
        {
            query = query.Where(index => index.IsImpersonated);
        }

        if (!string.IsNullOrWhiteSpace(filter.SessionId))
        {
            var sessionId = filter.SessionId.Trim();
            query = query.Where(index => index.SessionId == sessionId);
        }

        if (!string.IsNullOrWhiteSpace(filter.OrganizationId))
        {
            var organizationId = filter.OrganizationId.Trim();
            query = query.Where(index => index.OrganizationId == organizationId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Category))
        {
            var category = filter.Category.Trim();
            query = query.Where(index => index.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(filter.Name))
        {
            var name = filter.Name.Trim();
            query = query.Where(index => index.Name == name);
        }

        if (!string.IsNullOrWhiteSpace(filter.RecordId))
        {
            var recordId = filter.RecordId.Trim();
            query = query.Where(index => index.RecordId == recordId);
        }

        if (filter.From is { } from)
        {
            query = query.Where(index => index.CreatedUtc >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(index => index.CreatedUtc <= to);
        }

        return query;
    }

    private AuditTrailEventModel ToModel(AuditTrailEvent auditTrailEvent, bool includeData)
    {
        var descriptor = auditTrailManager.DescribeEvent(auditTrailEvent);
        var attribution = auditTrailEvent.As<AuditTrailAttribution>() ?? new AuditTrailAttribution();
        var subject = auditTrailEvent.As<AuditTrailSubject>() ?? new AuditTrailSubject();
        var nameText = descriptor.LocalizedName(services).Value;
        var categoryText = descriptor.LocalizedCategory(services).Value;

        JsonObject? data = null;
        if (includeData)
        {
            data = new JsonObject();
            foreach (var property in auditTrailEvent.Properties)
            {
                if (property.Key is nameof(AuditTrailAttribution) or nameof(AuditTrailSubject))
                {
                    continue;
                }

                data[property.Key] = property.Value?.DeepClone();
            }
        }

        var subjectText = subject.DisplayText ?? subject.RecordId ?? auditTrailEvent.CorrelationId;
        var summary = string.IsNullOrEmpty(subjectText)
            ? $"{categoryText}: {nameText}"
            : $"{categoryText}: {nameText} - {(subject.RecordType is { Length: > 0 } type ? type + " " : string.Empty)}{subjectText}";

        return new AuditTrailEventModel(
            auditTrailEvent.EventId,
            auditTrailEvent.Category,
            categoryText,
            auditTrailEvent.Name,
            nameText,
            auditTrailEvent.CorrelationId,
            auditTrailEvent.UserId,
            auditTrailEvent.UserName,
            auditTrailEvent.CreatedUtc,
            auditTrailEvent.ClientIpAddress,
            attribution.ActorKind,
            attribution.ClientId,
            attribution.Side,
            attribution.OrganizationId,
            attribution.FunctionalUserId,
            attribution.FunctionalUserName,
            attribution.ImpersonatedUserId,
            attribution.ImpersonatedUserName,
            attribution.SessionId,
            subject.RecordId ?? auditTrailEvent.CorrelationId,
            subject.RecordType,
            subject.OwnerId,
            subject.PartyIds ?? [],
            summary,
            data);
    }
}
