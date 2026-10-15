namespace Grove.Contents.OptionPicker.Api;

/// <summary>
/// A picker's request for rows.
/// </summary>
/// <remarks>
/// There is deliberately NO filter list here. Restrictions are read from the
/// attachment's settings server-side and resolved against <paramref name="EditorState"/>;
/// a client that could post its own filters could query any source with any predicate.
/// The client says what the USER has entered, never what the SERVER should allow.
/// </remarks>
public sealed record OptionSourceQueryRequest(
    string SourceKey,
    IReadOnlyList<string>? Columns,
    string? SearchText = null,
    string? ContentType = null,
    string? FieldName = null,
    IReadOnlyDictionary<string, string[]>? EditorState = null,
    IReadOnlyList<string>? SearchColumns = null,
    IReadOnlyList<string>? SortColumns = null,
    int Skip = 0,
    int Take = 50);

/// <summary>Rows for selected ids. The attachment (content type and field), when given, supplies
/// its label pattern and asks for edit links.</summary>
public sealed record OptionSourceResolveRequest(
    string SourceKey,
    IReadOnlyList<string>? Ids,
    IReadOnlyList<string>? Columns,
    string? ContentType = null,
    string? FieldName = null);

/// <summary>What a picker must watch, and what to do when it changes.</summary>
public sealed record OptionDependencyModel(
    IReadOnlyList<string> DependsOn,
    string OnParentChange,
    bool RequiredBecauseParentIs);

public sealed record OptionSelectionValidationRequest(
    string SourceKey,
    string? ContentType,
    string? FieldName,
    IReadOnlyList<string>? SelectedIds,
    IReadOnlyDictionary<string, string[]>? EditorState);

public sealed record OptionSelectionValidationModel(bool StillValid, string OnParentChange);
