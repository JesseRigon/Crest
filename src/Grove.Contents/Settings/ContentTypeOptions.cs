using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;
using Grove.Infrastructure.Settings;

namespace Grove.Contents.Settings;

/// <summary>The tenant's content types, for a settings field that names some (<c>[Setting(Options = typeof(ContentTypeOptions))]</c>).</summary>
public sealed class ContentTypeOptions(IContentDefinitionManager definitions) : ISettingOptions
{
    public async Task<IReadOnlyList<SettingOption>> GetOptionsAsync(CancellationToken cancellationToken = default) =>
        [.. (await definitions.ListTypeDefinitionsAsync())
            .OrderBy(type => type.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(type => new SettingOption(type.Name, type.DisplayName))];
}
