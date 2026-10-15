#nullable enable
using System.Globalization;
using Grove.Access;
using Grove.Modules;

namespace Grove.Media.Files;

/// <summary>
/// A tenant administrator opening another user's personal drive (docs/media.md § 7,
/// requirements 24-29). Tenant administrators hold every permission, so this is not a grant
/// but the deliberate step the file-object API insists on: without an access in force now,
/// another user's personal drive does not exist for them. Each access carries the tenant's
/// notice (sent first; a failure is recorded and the access proceeds), its delay (enforced
/// here: the access opens only after it), an optional override (its own permission and a
/// reason, recorded and put in the notice) and a duration from the tenant's list, after which
/// it ends by itself. Every step goes to the audit trail.
/// </summary>
public sealed class PersonalDriveAccessService(
    IPersonalDriveAccessStore accesses,
    IMediaObjectStore store,
    ICallerContextAccessor callers,
    IAccessDecision decision,
    IClock clock,
    IFileDriveSettingsAccessor settings,
    IPersonalDriveNotifier notifier,
    IEnumerable<IAccessAuditor> auditors) : IPersonalDriveAccessService
{
    public const string RequestOperation = "PersonalDrive.Request";
    public const string NoticeOperation = "PersonalDrive.Notice";
    public const string OverrideOperation = "PersonalDrive.Override";
    public const string OpenOperation = "PersonalDrive.Open";
    public const string EndOperation = "PersonalDrive.End";
    public const string CloseOperation = "PersonalDrive.Close";

    /// <summary>Marks what an administrator does inside the drive during an access.</summary>
    public const string AdministratorAccessReason = "administrator access";

    private readonly Dictionary<string, PersonalDriveAccess?> _current = new(StringComparer.Ordinal);

    public async Task<PersonalDriveAccess> RequestAsync(string driveId, TimeSpan? duration, bool overrideDelay, string? reason, CancellationToken cancellationToken = default)
    {
        var caller = Administrator();
        var drive = await OtherUsersDriveAsync(caller, driveId);
        var tenant = await settings.GetPersonalDriveAccessAsync();

        if (!tenant.Durations.Contains(duration))
        {
            throw new ArgumentException("Choose one of the tenant's access durations.", nameof(duration));
        }

        // A delay needs a notice to count from (requirement 26).
        var delay = tenant.Notice == PersonalDriveNotice.None ? TimeSpan.Zero : tenant.Delay;
        if (overrideDelay)
        {
            if (!tenant.AllowDelayOverride)
            {
                throw new MediaObjectConflictException("This tenant does not allow the notice delay to be overridden.");
            }

            await decision.RequireAsync(caller, MediaPermissions.OverridePersonalDriveNoticeDelay.Name, cancellationToken: cancellationToken);
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("Overriding the delay needs a reason.", nameof(reason));
            }
        }

        var now = Now();
        var opens = overrideDelay ? now : now + delay;
        var access = new PersonalDriveAccess(
            IdGenerator.GenerateId(),
            driveId,
            caller.UserId!,
            now,
            opens,
            duration is { } length ? opens + length : null,
            overrideDelay,
            overrideDelay ? reason!.Trim() : null,
            null);
        var document = new PersonalDriveAccessDocument { Access = access };

        await AuditAsync(caller, RequestOperation, driveId, $"opens {Format(access.OpensUtc)}, {(access.EndsUtc is { } ends ? $"ends {Format(ends)}" : "until closed")}");
        if (overrideDelay)
        {
            await AuditAsync(caller, OverrideOperation, driveId, access.Reason);
        }

        if (tenant.Notice != PersonalDriveNotice.None)
        {
            var subject = "An administrator is opening your drive";
            var body = $"{caller.UserName ?? caller.UserId} requested access to your personal drive \"{drive.Placement.Name}\". " +
                $"The access opens {Format(access.OpensUtc)} and {(access.EndsUtc is { } until ? $"ends {Format(until)}" : "lasts until it is closed")}." +
                (overrideDelay ? $" The notice delay was overridden: {access.Reason}" : string.Empty);
            var (sent, detail) = await notifier.NotifyAsync(drive.OwnerUserId, tenant.Notice, subject, body, cancellationToken);
            document.Notice = detail;

            // A notice that cannot be sent is recorded with the access, which proceeds (25).
            await AuditAsync(caller, NoticeOperation, driveId, detail, sent ? AccessVerdict.Allow : AccessVerdict.Deny);
        }

        await accesses.SaveAsync(document);
        _current.Remove(driveId);
        return access;
    }

    public async Task CloseAsync(string accessId, CancellationToken cancellationToken = default)
    {
        var caller = Administrator();
        var document = await accesses.GetAsync(accessId);
        if (document is null || document.Access.AdministratorUserId != caller.UserId)
        {
            throw new MediaObjectNotFoundException();
        }

        if (document.Access.ClosedUtc is not null)
        {
            return;
        }

        document.Access = document.Access with { ClosedUtc = Now() };
        document.EndRecorded = true;
        await accesses.SaveAsync(document);
        _current.Remove(document.Access.DriveId);
        await AuditAsync(caller, CloseOperation, document.Access.DriveId, $"closed by hand, access {accessId}");
    }

    public async Task<PersonalDriveAccess?> CurrentAsync(string driveId, CancellationToken cancellationToken = default)
    {
        if (callers.Current is not { IsSuperUser: true, UserId: { } userId } caller)
        {
            return null;
        }

        if (_current.TryGetValue(driveId, out var known))
        {
            return known;
        }

        var now = Now();
        PersonalDriveAccess? current = null;
        foreach (var document in await accesses.ListOpenAsync(driveId, userId))
        {
            var access = document.Access;
            if (access.EndsUtc is { } ends && ends <= now)
            {
                // Ended on time (requirement 28); recorded the first time it is noticed.
                if (!document.EndRecorded)
                {
                    document.EndRecorded = true;
                    await accesses.SaveAsync(document);
                    await AuditAsync(caller, EndOperation, driveId, $"ended {Format(ends)}, access {access.Id}");
                }

                continue;
            }

            if (access.OpensUtc > now)
            {
                continue;
            }

            if (!document.OpenRecorded)
            {
                document.OpenRecorded = true;
                await accesses.SaveAsync(document);
                await AuditAsync(caller, OpenOperation, driveId, $"access {access.Id}");
            }

            current = access;
        }

        _current[driveId] = current;
        return current;
    }

    /// <summary>Records an action an administrator takes inside the drive during an access.</summary>
    public Task RecordActionAsync(CallerContext caller, string permission, string placementId, PersonalDriveAccess access) =>
        AuditAsync(caller, permission, placementId, $"{AdministratorAccessReason} {access.Id}", isRead: permission == MediaPermissions.ViewMedia.Name);

    private CallerContext Administrator() =>
        callers.Current is { IsSuperUser: true, UserId: not null } caller
            ? caller
            : throw new AccessDeniedException(MediaPermissions.ManageSharedDrives.Name, "Only a tenant administrator opens another user's drive.");

    private async Task<(MediaPlacement Placement, string OwnerUserId)> OtherUsersDriveAsync(CallerContext caller, string driveId)
    {
        var placement = await store.GetPlacementAsync(driveId);
        var content = placement is null ? null : await store.GetContentAsync(placement.Placement.ContentId);
        if (placement is null || placement.Placement.ParentId is not null ||
            content?.Content is not { DriveType: DriveType.Personal, Owner: { Kind: MediaOwnerKind.User, Id: { } owner } } ||
            owner == caller.UserId)
        {
            throw new MediaObjectNotFoundException();
        }

        return (placement.Placement, owner);
    }

    private async Task AuditAsync(CallerContext caller, string operation, string driveOrPlacementId, string? reason, AccessVerdict verdict = AccessVerdict.Allow, bool isRead = false)
    {
        foreach (var auditor in auditors)
        {
            await auditor.RecordAsync(new AccessEvent(AccessEventKind.Execution, operation, caller, verdict, Resource: $"{FileShareableKind.Name}:{driveOrPlacementId}", Reason: reason, IsRead: isRead));
        }
    }

    private DateTimeOffset Now() => new(DateTime.SpecifyKind(clock.UtcNow, DateTimeKind.Utc));

    private static string Format(DateTimeOffset value) => value.ToString("u", CultureInfo.InvariantCulture);
}
