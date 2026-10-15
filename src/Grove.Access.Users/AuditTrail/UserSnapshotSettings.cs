#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grove.Entities;
using Grove.Infrastructure.Settings;
using Grove.Access.Users.AuditTrail.Handlers;
using Grove.Access.Users.AuditTrail.Models;
using Grove.Access.Users.AuditTrail.Services;
using Grove.Access.Users.Models;
using Grove.Access.Users.Services;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Access.Users.AuditTrail;

/// <summary>
/// Which properties of a user the audit trail's user snapshot keeps, and how each is redacted
/// (the settings group <see cref="GroupId"/>; stored as <see cref="AuditTrailUserEventSettings"/>).
/// </summary>
public sealed class UserSnapshotSettings
{
    public const string GroupId = "user-audit-trail";

    [Display(
        Name = "User properties to store",
        Description = "The properties of the user are stored in the event's user snapshot. A property set to \"Not stored\" is left out; \"Store\" keeps it as text; any other choice redacts it with that redactor.")]
    [Setting(Editor = "objects")]
    public List<UserSnapshotProperty> Properties { get; set; } = [];

    /// <summary>The user's properties a snapshot may keep: the user's own and the custom user settings, never the secrets.</summary>
    public static async Task<IReadOnlyList<string>> PropertyNamesAsync(IServiceProvider services)
    {
        var names = (JsonSerializer.SerializeToNode(new User()) as JsonObject)?.Select(pair => pair.Key) ?? [];
        if (services.GetService<CustomUserSettingsService>() is { } customSettings)
        {
            names = names.Union(await customSettings.GetAllSettingsTypeNamesAsync());
        }

        return [.. names.Where(name => !UserEventHandler.BannedProperties.Contains(name)).Order(StringComparer.Ordinal)];
    }

    public static async Task<UserSnapshotSettings> LoadAsync(IServiceProvider services)
    {
        var settings = await services.GetRequiredService<ISiteService>().GetSettingsAsync<AuditTrailUserEventSettings>();
        var stored = settings.UserSnapshotRedactors ?? [];
        var redactors = RedactorNames(services);

        return new UserSnapshotSettings
        {
            Properties = [.. (await PropertyNamesAsync(services)).Select(name => new UserSnapshotProperty
            {
                Name = name,
                Redactor = stored.TryGetValue(name, out var redactor) && redactors.Contains(redactor) ? redactor : nameof(RemoveRedactor),
            })],
        };
    }

    public static async Task SaveAsync(IServiceProvider services, UserSnapshotSettings model)
    {
        // Only the properties a snapshot may keep, with a registered redactor: a submission
        // cannot name a secret (the password hash) into the snapshot.
        var allowed = new HashSet<string>(await PropertyNamesAsync(services), StringComparer.Ordinal);
        var redactors = RedactorNames(services);
        var selected = model.Properties
            .Where(property => allowed.Contains(property.Name) && property.Redactor != nameof(RemoveRedactor) && redactors.Contains(property.Redactor))
            .GroupBy(property => property.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last().Redactor, StringComparer.Ordinal);

        var siteService = services.GetRequiredService<ISiteService>();
        var site = await siteService.LoadSiteSettingsAsync();
        var settings = site.GetOrCreate<AuditTrailUserEventSettings>();
        settings.UserSnapshotRedactors = selected;
        site.Put(settings);
        await siteService.UpdateSiteSettingsAsync(site);
    }

    internal static HashSet<string> RedactorNames(IServiceProvider services) =>
        new(services.GetServices<Redactor>().Where(redactor => redactor is not null).Select(redactor => redactor.GetType().Name), StringComparer.Ordinal);
}

/// <summary>One user property and how the snapshot keeps it.</summary>
public sealed class UserSnapshotProperty
{
    [Display(Name = "Property")]
    [Setting(ReadOnly = true)]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Stored as")]
    [Setting(Options = typeof(UserSnapshotRedactorOptions))]
    public string Redactor { get; set; } = nameof(RemoveRedactor);
}

/// <summary>The registered redactors: "Not stored", "Store" (as text), then each redactor by name.</summary>
public sealed class UserSnapshotRedactorOptions(IServiceProvider services) : ISettingOptions
{
    public Task<IReadOnlyList<SettingOption>> GetOptionsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SettingOption> options =
        [
            .. UserSnapshotSettings.RedactorNames(services)
                .Select(name => new SettingOption(name, name switch
                {
                    nameof(RemoveRedactor) => "Not stored",
                    nameof(NullRedactor) => "Store",
                    _ => name,
                }))
                .OrderBy(option => option.Value == nameof(RemoveRedactor) ? 0 : option.Value == nameof(NullRedactor) ? 1 : 2)
                .ThenBy(option => option.Text, StringComparer.Ordinal),
        ];

        return Task.FromResult(options);
    }
}
