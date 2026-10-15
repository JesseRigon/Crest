using Grove.Contents.Types;

namespace Grove.Services;

/// <summary>
/// Field visibility conditions compare keys: an option picker parent's stored ids resolve
/// through its source (<see cref="OptionParentValueResolver"/>); scalar fields pass through.
/// </summary>
public sealed class OptionPickerComparisonValueResolver(OptionParentValueResolver parentValues) : IFieldComparisonValueResolver
{
    public Task<IReadOnlyList<string>> ResolveAsync(string contentType, string path, string? column, IReadOnlyList<string> rawValues, CancellationToken cancellationToken = default) =>
        parentValues.ResolveAsync(contentType, path, column, rawValues, cancellationToken);
}
