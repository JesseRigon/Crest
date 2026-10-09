using Crest.ContentManagement.Metadata;
using Crest.Data.Migration;
using Crest.Environment.Shell;
using Crest.Environment.Shell.Descriptor.Models;
using Crest.Media.Fields;
using Crest.Media.Settings;

namespace Crest.Media;

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
        if (_shellDescriptor.WasFeatureAlreadyInstalled("Crest.Media"))
        {
            await UpgradeAsync();
        }

        // Shortcut other migration steps on new content definition schemas.
        return 1;
    }

    // Upgrade an existing installation.
    private Task UpgradeAsync()
        => _contentDefinitionManager.MigrateFieldSettingsAsync<MediaField, MediaFieldSettings>();
}
