using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Grove.AuditTrail.Indexes;
using Grove.AuditTrail.Models;
using Grove.AuditTrail.Services.Models;
using Grove.AuditTrail.Settings;
using Grove.Entities;
using Grove.Environment.Shell;
using Grove.Modules;
using Grove.Settings;
using YesSql;

namespace Grove.AuditTrail.Services;

public class AuditTrailManager : IAuditTrailManager
{
    private readonly IClock _clock;
    private readonly YesSql.ISession _session;
    private readonly ISiteService _siteService;
    private readonly AuditTrailOptions _auditTrailOptions;
    private readonly ILookupNormalizer _keyNormalizer;
    private readonly IAuditTrailIdGenerator _auditTrailIdGenerator;
    private readonly IEnumerable<IAuditTrailEventHandler> _auditTrailEventHandlers;
    private readonly IClientIPAddressAccessor _clientIPAddressAccessor;
    private readonly ShellSettings _shellSettings;
    private readonly ILogger _logger;

    public AuditTrailManager(
        IClock clock,
        YesSql.ISession session,
        ISiteService siteService,
        IOptions<AuditTrailOptions> auditTrailOptions,
        ILookupNormalizer keyNormalizer,
        IEnumerable<IAuditTrailEventHandler> auditTrailEventHandlers,
        IClientIPAddressAccessor clientIPAddressAccessor,
        IAuditTrailIdGenerator auditTrailIdGenerator,
        ShellSettings shellSettings,
        ILogger<AuditTrailManager> logger)
    {
        _clock = clock;
        _session = session;
        _siteService = siteService;
        _auditTrailOptions = auditTrailOptions.Value;
        _keyNormalizer = keyNormalizer;
        _auditTrailEventHandlers = auditTrailEventHandlers;
        _clientIPAddressAccessor = clientIPAddressAccessor;
        _auditTrailIdGenerator = auditTrailIdGenerator;
        _shellSettings = shellSettings;
        _logger = logger;
    }

    public async Task RecordEventAsync<TEvent>(AuditTrailContext<TEvent> context) where TEvent : class, new()
    {
        // The trail starts once the tenant is set up: nothing is recorded while the shell is
        // initializing. Setup runs every feature's migration in one pass, in dependency order, so
        // a write audited by a feature migrated before AuditTrail (roles, users, settings, content)
        // would queue an event for an audit store whose tables do not exist yet; the session's
        // flush then fails and rolls back the whole setup's schema transaction.
        if (_shellSettings.IsInitializing())
        {
            return;
        }

        var descriptor = DescribeEvent(context.Name, context.Category);
        if (descriptor == null || !await IsEventEnabledAsync(descriptor))
        {
            return;
        }

        var createContext = new AuditTrailCreateContext<TEvent>(
            context.Name,
            context.Category,
            context.CorrelationId,
            context.UserId,
            context.UserName,
            context.AuditTrailEventItem
        )
        {
            Subject = context.Subject,
            Attribution = context.Attribution,
        };

        await _auditTrailEventHandlers.InvokeAsync((handler, context) => handler.CreateAsync(context), createContext, _logger);

        var auditTrailEvent = new AuditTrailEvent
        {
            EventId = _auditTrailIdGenerator.GenerateUniqueId(),
            Category = createContext.Category,
            Name = createContext.Name,
            CorrelationId = createContext.CorrelationId,
            UserId = createContext.UserId,
            UserName = createContext.UserName ?? "",
            NormalizedUserName = string.IsNullOrEmpty(createContext.UserName) ? "" : _keyNormalizer.NormalizeName(createContext.UserName),
            ClientIpAddress = string.IsNullOrEmpty(createContext.ClientIpAddress)
                ? await GetClientIpAddressAsync()
                : createContext.ClientIpAddress,
            CreatedUtc = createContext.CreatedUtc ?? _clock.UtcNow,
        };

        auditTrailEvent.Put(createContext.AuditTrailEventItem);
        auditTrailEvent.Put(createContext.Attribution ?? new AuditTrailAttribution());
        auditTrailEvent.Put(createContext.Subject ?? new AuditTrailSubject());

        await _auditTrailEventHandlers.InvokeAsync((handler, context, auditTrailEvent) => handler.AlterAsync(context, auditTrailEvent), createContext, auditTrailEvent, _logger);

        // An actor handler may have moved the audit attribution (impersonation): the
        // functional attribution defaults to the actor, and the normalized name follows the name.
        auditTrailEvent.NormalizedUserName = string.IsNullOrEmpty(auditTrailEvent.UserName) ? "" : _keyNormalizer.NormalizeName(auditTrailEvent.UserName);
        auditTrailEvent.Alter<AuditTrailAttribution>(attribution =>
        {
            attribution.FunctionalUserId ??= string.IsNullOrEmpty(auditTrailEvent.UserId) ? null : auditTrailEvent.UserId;
            attribution.FunctionalUserName ??= string.IsNullOrEmpty(auditTrailEvent.UserName) ? null : auditTrailEvent.UserName;
            if (attribution.ActorKind == AuditTrailActorKinds.User && string.IsNullOrEmpty(auditTrailEvent.UserId) && string.IsNullOrEmpty(auditTrailEvent.UserName))
            {
                attribution.ActorKind = AuditTrailActorKinds.Anonymous;
            }
        });

        await _session.SaveAsync(auditTrailEvent, collection: AuditTrailEvent.Collection);
    }
    public Task<AuditTrailEvent> GetEventAsync(string eventId) =>
        _session.Query<AuditTrailEvent, AuditTrailEventIndex>(collection: AuditTrailEvent.Collection)
            .Where(index => index.EventId == eventId)
            .FirstOrDefaultAsync();

    public async Task<int> TrimEventsAsync(TimeSpan retentionPeriod)
    {
        var dateThreshold = _clock.UtcNow - retentionPeriod;

        var events = await _session.Query<AuditTrailEvent, AuditTrailEventIndex>(collection: AuditTrailEvent.Collection)
            .Where(index => index.CreatedUtc <= dateThreshold)
            .ListAsync();

        var deletedEvents = 0;
        foreach (var auditTrailEvent in events)
        {
            _session.Delete(auditTrailEvent, collection: AuditTrailEvent.Collection);
            deletedEvents++;
        }

        return deletedEvents;
    }

    public AuditTrailEventDescriptor DescribeEvent(AuditTrailEvent auditTrailEvent) =>
        DescribeEvent(auditTrailEvent.Name, auditTrailEvent.Category)
        ?? AuditTrailEventDescriptor.Default(auditTrailEvent);

    public IEnumerable<AuditTrailCategoryDescriptor> DescribeCategories()
        => _auditTrailOptions.CategoryDescriptors.Values;

    public AuditTrailCategoryDescriptor DescribeCategory(string name)
    {
        if (_auditTrailOptions.CategoryDescriptors.TryGetValue(name, out var category))
        {
            return category;
        }

        return AuditTrailCategoryDescriptor.Default(name);
    }

    private AuditTrailEventDescriptor DescribeEvent(string name, string categoryName)
    {
        var category = DescribeCategory(categoryName);
        category.Events.TryGetValue(name, out var descriptor);

        return descriptor;
    }

    private async Task<string> GetClientIpAddressAsync()
    {
        var siteSettings = await _siteService.GetSiteSettingsAsync();

        if (!siteSettings.TryGet<AuditTrailSettings>(out var settings) ||
            !settings.ClientIpAddressAllowed)
        {
            return null;
        }

        return (await _clientIPAddressAccessor.GetIPAddressAsync())?.ToString();
    }

    private async Task<bool> IsEventEnabledAsync(AuditTrailEventDescriptor descriptor)
    {
        if (descriptor.IsMandatory)
        {
            return true;
        }

        var siteSettings = await _siteService.GetSiteSettingsAsync();

        if (!siteSettings.TryGet<AuditTrailSettings>(out var settings))
        {
            return descriptor.IsEnabledByDefault;
        }

        var eventSettings = settings.Categories
            .FirstOrDefault(category => category.Name == descriptor.Category)?.Events
            .FirstOrDefault(settings => settings.Name == descriptor.Name);

        return eventSettings?.IsEnabled ?? descriptor.IsEnabledByDefault;
    }
}
