using Grove.Contents.Types;

namespace Grove.Services;

/// <summary>
/// Field conditions compare keys: an option list field's stored ids resolve through its source
/// (<see cref="OptionParentValueResolver"/>), and its keys compare case-insensitively, so they come
/// back lower-cased and marked so; scalar fields pass through.
/// </summary>
public sealed class OptionListFieldComparisonValueResolver(OptionParentValueResolver parentValues) : IFieldComparisonValueResolver
{
    public async Task<FieldComparisonValues> ResolveAsync(string contentType, string path, string? column, IReadOnlyList<string> rawValues, CancellationToken cancellationToken = default)
    {
        var (values, isOptionField) = await parentValues.ResolveFieldAsync(contentType, path, column, rawValues, cancellationToken);
        return isOptionField && OptionParentValueResolver.IsKeyColumn(column)
            ? new FieldComparisonValues([.. values.Select(value => value.ToLowerInvariant())], IgnoreCase: true)
            : new FieldComparisonValues(values);
    }
}
