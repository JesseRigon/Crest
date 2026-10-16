using Grove.Fields;
using Grove.Models;
using Grove.Infrastructure.Settings;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;

namespace Grove.Services;

/// <summary>
/// Turns a parent field's RAW values into COMPARISON values. A scalar parent (text,
/// numeric, boolean) already stores what logic compares, so its values pass through.
/// An OptionListField parent stores ids - per the identity rule content stores ids
/// while CODE COMPARES KEYS - so its selection is resolved through its own source's
/// GetByIdsAsync and a column is read from the rows (Key by default). This is the
/// seam both cascading filters and field-visibility conditions resolve through.
/// </summary>
public sealed class OptionParentValueResolver(
    IEnumerable<IOptionSourceProvider> sourceProviders,
    IContentDefinitionManager contentDefinitionManager)
{
    /// <summary>The column name meaning "the option's technical key" (the default).</summary>
    public const string KeyColumn = OptionSourceKeys.KeyColumn;

    /// <summary>
    /// Resolves the comparison values <paramref name="rawValues"/> stand for. Paths
    /// are "Field" (a field of the content type's own field set) or "FieldSet.Field" (by the
    /// type's attachment name). An unknown parent field, or a picker parent whose provider is
    /// missing, resolves to EMPTY - failing closed, so a dependent restriction never silently
    /// widens into "matches everything".
    /// </summary>
    public async Task<IReadOnlyList<string>> ResolveAsync(
        string contentType,
        string path,
        string? column,
        IReadOnlyList<string> rawValues,
        CancellationToken cancellationToken = default) =>
        (await ResolveFieldAsync(contentType, path, column, rawValues, cancellationToken)).Values;

    /// <summary>As <see cref="ResolveAsync"/>, also saying whether the parent is an option list field (whose values are option rows' columns).</summary>
    public async Task<(IReadOnlyList<string> Values, bool IsOptionField)> ResolveFieldAsync(
        string contentType,
        string path,
        string? column,
        IReadOnlyList<string> rawValues,
        CancellationToken cancellationToken = default)
    {
        var field = await FindFieldAsync(contentType, path);
        if (field is null)
        {
            return ([], false);
        }

        if (!string.Equals(field.FieldTypeDefinition?.Name, nameof(OptionListField), StringComparison.Ordinal))
        {
            // Scalar parents: the stored value IS the comparison value.
            return (rawValues, false);
        }

        if (rawValues.Count == 0)
        {
            return (rawValues, true);
        }

        var sourceKey = field.GetSettings<OptionListFieldSettings>()?.SourceKey;
        if (string.IsNullOrWhiteSpace(sourceKey))
        {
            return ([], true);
        }

        var (providerKey, qualifier) = OptionSourceKeys.Split(sourceKey);
        var provider = sourceProviders.FirstOrDefault(candidate =>
            string.Equals(candidate.Key, providerKey, StringComparison.OrdinalIgnoreCase));
        if (provider is null)
        {
            return ([], true);
        }

        var wantsKey = IsKeyColumn(column);
        var rows = await provider.GetByIdsAsync(qualifier, rawValues, wantsKey ? [] : [column!], cancellationToken);
        return (Project(rows, column), true);
    }

    /// <summary>
    /// Projects resolved parent rows to the comparison column. Pure so the id-to-key
    /// translation - the crux of the cascade fix - is testable without providers.
    /// Null or "Key" reads the option's technical Key, falling back to the row id
    /// for entity sources whose id is their identity.
    /// </summary>
    internal static IReadOnlyList<string> Project(IReadOnlyList<OptionRow> rows, string? column)
    {
        var wantsKey = IsKeyColumn(column);

        return [.. rows
            .Select(row => wantsKey
                ? (string.IsNullOrWhiteSpace(row.Key) ? row.Id : row.Key)
                : (row.Values.TryGetValue(column!, out var value) ? value : null))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    internal static bool IsKeyColumn(string? column) =>
        string.IsNullOrWhiteSpace(column) || string.Equals(column, KeyColumn, StringComparison.OrdinalIgnoreCase);

    private async Task<ContentFieldDefinition?> FindFieldAsync(string contentType, string path)
    {
        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length is 0 or > 2)
        {
            return null;
        }

        // "Field" addresses the content type's own field set (named after the type); a qualified
        // path names one of the type's attachments, whose field set may be named otherwise.
        var attachmentName = segments.Length == 2 ? segments[0] : contentType;
        var type = await contentDefinitionManager.GetTypeDefinitionAsync(contentType);
        var fieldSet = type?.FieldSets.FirstOrDefault(candidate => string.Equals(candidate.Name, attachmentName, StringComparison.OrdinalIgnoreCase))?.FieldSetDefinition
            ?? await contentDefinitionManager.GetFieldSetDefinitionAsync(attachmentName);

        return fieldSet?.Fields.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, segments[^1], StringComparison.OrdinalIgnoreCase));
    }
}
