using Crest.Security.Permissions;

namespace Crest.Media;

public static class MediaPermissions
{
    public static readonly Permission ManageMediaFolder = new("ManageMediaFolder", "Manage All Media Folders");

    public static readonly Permission ManageOthersMedia = new("ManageOthersMediaContent", "Manage Media For Others", [ManageMediaFolder]);

    public static readonly Permission ManageOwnMedia = new("ManageOwnMediaContent", "Manage Own Media", [ManageOthersMedia]);

    public static readonly Permission ManageAttachedMediaFieldsFolder = new("ManageAttachedMediaFieldsFolder", "Manage Attached Media Fields Folder", [ManageMediaFolder]);

    public static readonly Permission ManageMedia = new("ManageMediaContent", "Manage Media", [ManageOwnMedia, ManageAttachedMediaFieldsFolder]);

    /// <summary>Create and update file objects (sharing's Editor level); implied by managing them.</summary>
    public static readonly Permission EditMedia = new("EditMediaContent", "Create and edit media content", [ManageMedia]);

    /// <summary>Creating shared drives and assigning their Drive Admins (docs/media.md, requirement 31).
    /// Gives no access to a drive's contents.</summary>
    public static readonly Permission ManageSharedDrives = new("ManageSharedDrives", "Manage shared drives", isSecurityCritical: true);

    /// <summary>Opening another user's personal drive at once, skipping the tenant's notice delay,
    /// with a reason (requirement 27).</summary>
    public static readonly Permission OverridePersonalDriveNoticeDelay = new("OverridePersonalDriveNoticeDelay", "Override the personal drive notice delay", isSecurityCritical: true);

    public static readonly Permission UploadRestrictedMedia = new(
        "UploadRestrictedMedia",
        "Upload media file extensions requiring additional permission",
        isSecurityCritical: true);

    public static readonly Permission ManageMediaProfiles = new("ManageMediaProfiles", "Manage Media Profiles");

    public static readonly Permission ViewMediaOptions = new("ViewMediaOptions", "View Media Options");

    public static readonly Permission ManageMediaApiSettings = new("ManageMediaApiSettings", "Manage Media API authentication settings");

    public static readonly Permission ManageAssetCache = new("ManageAssetCache", "Manage Asset Cache Folder");

    // Note: The ManageMediaFolder permission grants all access, so viewing must be implied by it too.
    public static readonly Permission ViewMedia = new("ViewMediaContent", "View media content in all folders", new[] { ManageMediaFolder });

    public static readonly Permission ViewRootMedia = new("ViewRootMediaContent", "View media content in the root folder", new[] { ViewMedia });

    public static readonly Permission ViewOthersMedia = new("ViewOthersMediaContent", "View others media content", new[] { ManageMediaFolder });

    public static readonly Permission ViewOwnMedia = new("ViewOwnMediaContent", "View own media content", new[] { ViewOthersMedia });
}
