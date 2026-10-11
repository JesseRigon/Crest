#nullable enable
using Crest.Entities;
using Crest.Settings;

namespace Crest.Media.Files;

/// <summary>Which drive types a tenant uses (requirement 18). Shared drives always exist;
/// organization and personal drives are off by default.</summary>
public sealed class FileDriveSettings
{
    public bool OrganizationDrives { get; set; }

    public bool PersonalDrives { get; set; }

    /// <summary>The role an organization's admins hold while acting for it; granted Admin on
    /// its drive, scoped to the organization (requirement 17).</summary>
    public string OrganizationAdminRole { get; set; } = "Member Administrator";

    public bool Allows(DriveType type) => type switch
    {
        DriveType.Shared => true,
        DriveType.Organization => OrganizationDrives,
        DriveType.Personal => PersonalDrives,
        _ => false,
    };
}

/// <summary>The file system's tenant settings, kept as site settings sections:
/// <see cref="FileDriveSettings"/> and <see cref="PersonalDriveAccessSettings"/>.</summary>
public interface IFileDriveSettingsAccessor
{
    Task<FileDriveSettings> GetAsync();

    Task<PersonalDriveAccessSettings> GetPersonalDriveAccessAsync();

    /// <summary>Saves the sections given (null leaves one as it is).</summary>
    Task SaveAsync(FileDriveSettings? drives, PersonalDriveAccessSettings? personalDriveAccess);
}

public sealed class SiteFileDriveSettingsAccessor(ISiteService sites) : IFileDriveSettingsAccessor
{
    public Task<FileDriveSettings> GetAsync() => sites.GetSettingsAsync<FileDriveSettings>();

    public Task<PersonalDriveAccessSettings> GetPersonalDriveAccessAsync() => sites.GetSettingsAsync<PersonalDriveAccessSettings>();

    public async Task SaveAsync(FileDriveSettings? drives, PersonalDriveAccessSettings? personalDriveAccess)
    {
        var site = await sites.LoadSiteSettingsAsync();
        if (drives is not null)
        {
            site.Put(drives);
        }

        if (personalDriveAccess is not null)
        {
            site.Put(personalDriveAccess);
        }

        await sites.UpdateSiteSettingsAsync(site);
    }
}
