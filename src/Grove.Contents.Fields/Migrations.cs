using Grove.Contents.Fields.Fields;
using Grove.Contents.Fields.Settings;
using Grove.Contents.Management.Metadata;
using Grove.Data.Migration;
using Grove.Environment.Shell;
using Grove.Environment.Shell.Descriptor.Models;

namespace Grove.Contents.Fields;

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
        if (_shellDescriptor.WasFeatureAlreadyInstalled("Grove.Contents.Fields"))
        {
            await UpgradeAsync();
        }

        // Shortcut other migration steps on new content definition schemas.
        return 3;
    }

    // Upgrade an existing installation.
    private async Task UpgradeAsync()
    {
        // Boolean field
        await _contentDefinitionManager.MigrateFieldSettingsAsync<BooleanField, BooleanFieldSettings>();

        // Date field
        await _contentDefinitionManager.MigrateFieldSettingsAsync<DateField, DateFieldSettings>();

        // Date time field
        await _contentDefinitionManager.MigrateFieldSettingsAsync<DateTimeField, DateTimeFieldSettings>();

        // Html field
        await _contentDefinitionManager.MigrateFieldSettingsAsync<HtmlField, HtmlFieldSettings>();

        // Link field
        await _contentDefinitionManager.MigrateFieldSettingsAsync<LinkField, LinkFieldSettings>();

        // MultiText field
        await _contentDefinitionManager.MigrateFieldSettingsAsync<MultiTextField, MultiTextFieldSettings>();

        // Numeric field
        await _contentDefinitionManager.MigrateFieldSettingsAsync<NumericField, NumericFieldSettings>();

        // Text field
        await _contentDefinitionManager.MigrateFieldSettingsAsync<TextField, TextFieldHeaderDisplaySettings>();
        await _contentDefinitionManager.MigrateFieldSettingsAsync<TextField, TextFieldPredefinedListEditorSettings>();
        await _contentDefinitionManager.MigrateFieldSettingsAsync<TextField, TextFieldSettings>();

        // Time field
        await _contentDefinitionManager.MigrateFieldSettingsAsync<TimeField, TimeFieldSettings>();

        // YouTube field
        await _contentDefinitionManager.MigrateFieldSettingsAsync<YoutubeField, YoutubeFieldSettings>();
    }

    // Previously, Liquid rendering was enabled by not having Html sanitization enabled and UpdateFrom1Async disabled
    // sanitization to ensure that HtmlFields kept Liquid rendering enabled. Since Liquid rendering is now controlled
    // by a separate setting, disabling sanitization is no longer necessary.

    public async Task<int> UpdateFrom2Async()
    {
        // To keep the same behavior as before, RenderLiquid is initialized to the opposite of SanitizeHtml.
        var partDefinitions = await _contentDefinitionManager.LoadFieldSetDefinitionsAsync();
        foreach (var partDefinition in partDefinitions)
        {
            if (partDefinition.Fields.Any(f => f.FieldTypeDefinition.Name == "HtmlField"))
            {
                await _contentDefinitionManager.AlterFieldSetDefinitionAsync(partDefinition.Name, partBuilder =>
                {
                    foreach (var fieldDefinition in partDefinition.Fields.Where(f => f.FieldTypeDefinition.Name == "HtmlField"))
                    {
                        partBuilder.WithField(fieldDefinition.Name, fieldBuilder =>
                        {
                            fieldBuilder.MergeSettings<HtmlFieldSettings>(s => s.RenderLiquid = !s.SanitizeHtml);
                        });
                    }
                });
            }
        }

        return 3;
    }
}
