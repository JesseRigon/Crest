using Grove.Environment.Shell.Scope;
using Grove.Media.Library;
using Microsoft.Extensions.DependencyInjection;
using Grove.ContentManagement.Metadata;
using Grove.Data.Migration;
using Grove.Environment.Shell;
using Grove.Environment.Shell.Descriptor.Models;
using Grove.Media.Fields;
using Grove.Media.Settings;

namespace Grove.Media;

public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ShellDescriptor _shellDescriptor;

    public Migrations(
        IContentDefinitionManager contentDefinitionManager,
        ShellDescriptor shellDescriptor)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _shellDescriptor = shellDescriptor;
    }

    // New installations don't need to be upgraded, but because there is no initial migration record,
    // 'UpgradeAsync()' is called in a new 'CreateAsync()' but only if the feature was already installed.
    public async Task<int> CreateAsync()
    {
        if (_shellDescriptor.WasFeatureAlreadyInstalled("Grove.Media"))
        {
            await UpgradeAsync();
        }

        // Shortcut other migration steps on new content definition schemas.
        return 1;
    }

    /// <summary>
    /// The media library (docs/media.md, ruling F1): after this tenant's scope completes, the
    /// <c>/media</c> tree is imported into the "Media library" shared drive, its rights move to
    /// sharing, <c>/media</c> is served from the drive, and media fields get placement ids. A new
    /// tenant gets an empty library. Existing files are scanned unless the tenant is configured
    /// to trust them (<c>Grove_Media:Library:TrustExistingFiles</c>); the <c>MediaLibraryImport</c>
    /// recipe step runs it again.
    /// </summary>
    public static int UpdateFrom1()
    {
        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<MediaLibraryImporter>().ImportAsync());
        return 2;
    }

    // Upgrade an existing installation.
    private Task UpgradeAsync()
        => _contentDefinitionManager.MigrateFieldSettingsAsync<MediaField, MediaFieldSettings>();
}
