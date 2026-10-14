using Crest.AuditTrail.Services;
using Crest.AuditTrail.Settings;
using Crest.Entities;
using Crest.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crest.AuditTrail.Api;

public sealed record AuditEventSettingModel(string Name, string Category, string DisplayName, string? Description, bool IsEnabled, bool IsMandatory);

public sealed record AuditCategorySettingModel(string Name, string DisplayName, IReadOnlyList<AuditEventSettingModel> Events);

/// <param name="Categories">Every registered event, grouped by category, with whether it is recorded.</param>
/// <param name="RetentionDays">How long events are kept before the trimming task removes them.</param>
/// <param name="TrimmingDisabled">The trimming task never removes anything.</param>
/// <param name="LastTrimmedUtc">When the trimming task last ran.</param>
public sealed record AuditTrailSettingsModel(
    IReadOnlyList<AuditCategorySettingModel> Categories,
    bool ClientIpAddressAllowed,
    int RetentionDays,
    bool TrimmingDisabled,
    DateTime? LastTrimmedUtc);

/// <param name="EnabledEvents">"Category/Name" of every event to record; mandatory events are recorded regardless.</param>
public sealed record AuditTrailSettingsUpdate(IReadOnlyList<string> EnabledEvents, bool ClientIpAddressAllowed, int RetentionDays, bool TrimmingDisabled);

/// <summary>The audit trail settings (the native Audit › Settings page): which events are recorded and how long they are kept.</summary>
[ApiController]
[AutoValidateAntiforgeryToken]
[Route(AuditTrailRoutes.Api + "/settings")]
public sealed class AuditTrailSettingsApiController(
    IAuthorizationService authorization,
    ISiteService siteService,
    IAuditTrailManager auditTrailManager,
    IServiceProvider services) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AuditTrailSettingsModel>> GetAsync()
    {
        if (!await authorization.AuthorizeAsync(User, AuditTrailPermissions.ManageAuditTrailSettings))
        {
            return Forbid();
        }

        return Ok(ToModel(await siteService.GetSiteSettingsAsync()));
    }

    [HttpPut]
    public async Task<ActionResult<AuditTrailSettingsModel>> PutAsync([FromBody] AuditTrailSettingsUpdate update)
    {
        if (!await authorization.AuthorizeAsync(User, AuditTrailPermissions.ManageAuditTrailSettings))
        {
            return Forbid();
        }

        if (update.RetentionDays < 0)
        {
            return BadRequest("The retention must be zero or more days.");
        }

        var enabled = (update.EnabledEvents ?? []).ToHashSet(StringComparer.Ordinal);
        var site = await siteService.LoadSiteSettingsAsync();
        site.Alter<AuditTrailSettings>(settings =>
        {
            settings.Categories = [.. auditTrailManager.DescribeCategories().Select(category => new AuditTrailCategorySettings
            {
                Name = category.Name,
                Events = [.. category.Events.Values.Select(auditEvent => new AuditTrailEventSettings
                {
                    Name = auditEvent.Name,
                    Category = auditEvent.Category,
                    IsEnabled = auditEvent.IsMandatory || enabled.Contains($"{auditEvent.Category}/{auditEvent.Name}"),
                })],
            })];
            settings.ClientIpAddressAllowed = update.ClientIpAddressAllowed;
        });
        site.Alter<AuditTrailTrimmingSettings>(trimming =>
        {
            trimming.RetentionDays = update.RetentionDays;
            trimming.Disabled = update.TrimmingDisabled;
        });
        await siteService.UpdateSiteSettingsAsync(site);

        return Ok(ToModel(site));
    }

    private AuditTrailSettingsModel ToModel(ISite site)
    {
        var settings = site.GetOrCreate<AuditTrailSettings>();
        var trimming = site.GetOrCreate<AuditTrailTrimmingSettings>();
        var stored = settings.Categories
            .SelectMany(category => category.Events)
            .GroupBy(setting => $"{setting.Category}/{setting.Name}", StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().IsEnabled, StringComparer.Ordinal);

        var categories = auditTrailManager.DescribeCategories()
            .Select(category => new AuditCategorySettingModel(
                category.Name,
                category.LocalizedName(services).ToString(),
                [.. category.Events.Values.Select(auditEvent => new AuditEventSettingModel(
                    auditEvent.Name,
                    auditEvent.Category,
                    auditEvent.LocalizedName(services).ToString(),
                    auditEvent.Description(services)?.ToString(),
                    auditEvent.IsMandatory || (stored.TryGetValue($"{auditEvent.Category}/{auditEvent.Name}", out var isEnabled) ? isEnabled : auditEvent.IsEnabledByDefault),
                    auditEvent.IsMandatory))]))
            .ToArray();

        return new AuditTrailSettingsModel(categories, settings.ClientIpAddressAllowed, trimming.RetentionDays, trimming.Disabled, trimming.LastRunUtc);
    }
}
