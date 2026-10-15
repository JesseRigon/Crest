namespace Grove.Media.Files;

/// <summary>How the owner of a personal drive is told an administrator opened it (requirement 25).</summary>
[Flags]
public enum PersonalDriveNotice
{
    None = 0,
    Email = 1,
    Portal = 2,
}

/// <summary>A tenant's settings for administrator access to personal drives (docs/media.md § 7).</summary>
public sealed class PersonalDriveAccessSettings
{
    public PersonalDriveNotice Notice { get; set; } = PersonalDriveNotice.None;

    /// <summary>Time between the notice and the access; requires a notice.</summary>
    public TimeSpan Delay { get; set; } = TimeSpan.Zero;

    /// <summary>Whether holders of the override permission may skip the delay, with a reason.</summary>
    public bool AllowDelayOverride { get; set; }

    /// <summary>The durations offered; null is "until closed".</summary>
    public List<TimeSpan?> Durations { get; set; } = [TimeSpan.FromMinutes(30), TimeSpan.FromHours(1), TimeSpan.FromMinutes(90), TimeSpan.FromHours(2), null];
}

public sealed record PersonalDriveAccess(string Id, string DriveId, string AdministratorUserId, DateTimeOffset RequestedUtc, DateTimeOffset OpensUtc, DateTimeOffset? EndsUtc, bool Overridden, string? Reason, DateTimeOffset? ClosedUtc);

/// <summary>
/// A tenant administrator opening another user's personal drive: always a separate, deliberate,
/// logged step, with the tenant's notice, delay, override and duration (requirements 24-29).
/// </summary>
public interface IPersonalDriveAccessService
{
    Task<PersonalDriveAccess> RequestAsync(string driveId, TimeSpan? duration, bool overrideDelay, string? reason, CancellationToken cancellationToken = default);

    Task CloseAsync(string accessId, CancellationToken cancellationToken = default);

    /// <summary>The caller's access to the drive in force now, if any.</summary>
    Task<PersonalDriveAccess?> CurrentAsync(string driveId, CancellationToken cancellationToken = default);
}
