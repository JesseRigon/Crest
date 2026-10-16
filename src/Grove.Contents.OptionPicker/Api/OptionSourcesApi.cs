using Grove.Api;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;
using Grove.Models;
using Grove.Permissions;
using Grove.Services;
using Grove.Infrastructure.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Grove.Contents.OptionPicker.Api;

/// <summary>
/// The provider-generic half of the picker API (<c>api/grove/option-sources</c>): rows, columns,
/// dependencies and selection validation for whatever source a field is bound to (field set
/// lists, users, content items, ...). The option list management is the
/// Grove.Contents.OptionLists feature.
/// </summary>
public sealed class OptionSourcesApi : ApiContributor
{
    public override string Name => "OptionSources";

    public override string FeatureId => OptionPickerConstants.FeatureId;

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("option-sources");
        routes.MapGet("", ApiHandlers.Handle<OptionSourcesHandlers>(h => h.ListSourcesAsync()));
        routes.MapPost("query", (HttpContext http, [FromBody] OptionSourceQueryRequest request) => ApiHandlers.For<OptionSourcesHandlers>(http).QuerySourceAsync(request));
        routes.MapGet("dependencies", (HttpContext http, [FromQuery] string contentType, [FromQuery] string fieldName) => ApiHandlers.For<OptionSourcesHandlers>(http).GetDependenciesAsync(contentType, fieldName));
        routes.MapPost("validate-selection", (HttpContext http, [FromBody] OptionSelectionValidationRequest request) => ApiHandlers.For<OptionSourcesHandlers>(http).ValidateSelectionAsync(request));
        routes.MapPost("resolve", (HttpContext http, [FromBody] OptionSourceResolveRequest request) => ApiHandlers.For<OptionSourcesHandlers>(http).ResolveSourceAsync(request));
        routes.MapPost("create", (HttpContext http, [FromBody] OptionSourceCreateRequest request) => ApiHandlers.For<OptionSourcesHandlers>(http).CreateAsync(request));
        routes.MapGet("create-links", (HttpContext http, [FromQuery] string contentType, [FromQuery] string fieldName) => ApiHandlers.For<OptionSourcesHandlers>(http).GetCreateLinksAsync(contentType, fieldName));
        routes.MapGet("{sourceKey}/columns", (HttpContext http, [FromRoute] string sourceKey) => ApiHandlers.For<OptionSourcesHandlers>(http).DescribeColumnsAsync(sourceKey));
    }
}

public sealed class OptionSourcesHandlers(
    IEnumerable<IOptionSourceProvider> sourceProviders,
    IContentDefinitionManager contentDefinitionManager,
    OptionParentValueResolver parentValues,
    IAuthorizationService authorizationService) : ApiHandlers
{
    /// <summary>
    /// Rows for a picker: whichever columns the caller asks for, from whichever source the field
    /// is bound to. This is the endpoint the option picker component queries as the user types.
    /// </summary>
    public async Task<IResult> QuerySourceAsync(OptionSourceQueryRequest request)
    {
        if (!await CanViewAsync())
        {
            return Forbid();
        }

        var (providerKey, qualifier) = OptionSourceKeys.Split(request.SourceKey);
        var provider = FindProvider(providerKey);
        if (provider is null)
        {
            return Problem($"There is no option source provider named '{providerKey}'.", statusCode: StatusCodes.Status400BadRequest);
        }

        // Filters come from the attachment's settings, resolved here against the editor state
        // the caller supplies - never from filters the caller sends: a client that could post its
        // own filters could query any source with any predicate.
        var resolution = await ResolveFiltersAsync(request.ContentType, request.FieldName, request.EditorState);

        // A required parent that has not been chosen means nothing to offer.
        if (resolution.HasUnmetDependencies)
        {
            return Ok(Array.Empty<OptionRow>());
        }

        // Sort is an instance parameter: the request's explicit sort wins, else the attachment's
        // configured SortColumns, else the provider's natural order. Sort columns and filter
        // paths are unioned into the requested columns so their values are materialized.
        var settings = await GetFieldSettingsAsync(request.ContentType, request.FieldName);
        var sortColumns = request.SortColumns is { Count: > 0 }
            ? request.SortColumns
            : settings?.SortColumns ?? [];
        var columns = (request.Columns ?? [])
            .Union(sortColumns, StringComparer.OrdinalIgnoreCase)
            .Union(FilterPaths(settings), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var rows = await provider.QueryAsync(
            new OptionSourceQuery(
                qualifier,
                columns,
                request.SearchText,
                resolution.Filters,
                request.SearchColumns,
                sortColumns,
                request.Skip,
                Math.Clamp(request.Take, 1, 200),
                settings?.TitlePattern),
            HttpContext.RequestAborted);

        return Ok(rows.ToArray());
    }

    /// <summary>
    /// Which fields a picker must watch, and whether it is required because its parent is. The
    /// editor asks once when it renders, then re-queries when a watched field commits.
    /// </summary>
    public async Task<IResult> GetDependenciesAsync(string contentType, string fieldName)
    {
        if (!await CanViewAsync())
        {
            return Forbid();
        }

        var settings = await GetFieldSettingsAsync(contentType, fieldName);
        if (settings is null)
        {
            return NotFound();
        }

        var partDefinition = await contentDefinitionManager.GetFieldSetDefinitionAsync(contentType);

        return Ok(new OptionDependencyModel(
            OptionFilterResolver.DependencyPaths(settings.Filters),
            settings.Filters.FirstOrDefault(filter => filter.IsDependent)?.OnParentChange
                ?? OptionParentChangeBehaviors.WarnThenClear,
            // Derived, not configured: a child whose parent is required is required too.
            OptionFilterResolver.IsRequiredBecauseParentIs(settings.Filters, path =>
                IsFieldRequired(partDefinition, path))));
    }

    /// <summary>
    /// Whether the currently-selected ids survive the editor's new state - the check behind
    /// clearing a child when its parent changes. Answered server-side because only the server
    /// knows the attachment's filters.
    /// </summary>
    public async Task<IResult> ValidateSelectionAsync(OptionSelectionValidationRequest request)
    {
        if (!await CanViewAsync())
        {
            return Forbid();
        }

        var (providerKey, qualifier) = OptionSourceKeys.Split(request.SourceKey);
        var provider = FindProvider(providerKey);
        if (provider is null)
        {
            return Problem($"There is no option source provider named '{providerKey}'.", statusCode: StatusCodes.Status400BadRequest);
        }

        var settings = await GetFieldSettingsAsync(request.ContentType, request.FieldName);
        var resolution = settings is null
            ? OptionFilterResolution.Unrestricted
            : await ResolveTranslatedAsync(request.ContentType!, settings, request.EditorState);
        var ids = request.SelectedIds ?? [];

        // Resolve by id rather than by query: a stale child may well be absent from the filtered
        // list, which is precisely what makes it stale.
        var rows = ids.Count == 0
            ? []
            : await provider.GetByIdsAsync(qualifier, ids, FilterPaths(settings), HttpContext.RequestAborted);

        return Ok(new OptionSelectionValidationModel(
            OptionFilterResolver.SelectionStillValid(resolution, ids, rows),
            settings?.Filters.FirstOrDefault(filter => filter.IsDependent)?.OnParentChange
                ?? OptionParentChangeBehaviors.WarnThenClear));
    }

    /// <summary>Rows for already-selected ids, so a picker can render what is stored. Batched:
    /// one call for every id on the field.</summary>
    public async Task<IResult> ResolveSourceAsync(OptionSourceResolveRequest request)
    {
        if (!await CanViewAsync())
        {
            return Forbid();
        }

        var (providerKey, qualifier) = OptionSourceKeys.Split(request.SourceKey);
        var provider = FindProvider(providerKey);
        if (provider is null)
        {
            return Problem($"There is no option source provider named '{providerKey}'.", statusCode: StatusCodes.Status400BadRequest);
        }

        var settings = await GetFieldSettingsAsync(request.ContentType, request.FieldName);
        var rows = await provider.GetByIdsAsync(
            new OptionSourceLookup(qualifier, request.Ids ?? [], request.Columns ?? [], settings?.TitlePattern, IncludeEditUrls: settings is not null),
            HttpContext.RequestAborted);
        return Ok(rows.ToArray());
    }

    /// <summary>The attachment's create links: for a source whose records can be created from the
    /// picker (content items), one per allowed type the caller may create.</summary>
    public async Task<IResult> GetCreateLinksAsync(string contentType, string fieldName)
    {
        if (!await CanViewAsync())
        {
            return Forbid();
        }

        var settings = await GetFieldSettingsAsync(contentType, fieldName);
        if (settings is null)
        {
            return NotFound();
        }

        var (providerKey, qualifier) = OptionSourceKeys.Split(settings.SourceKey);
        return FindProvider(providerKey) is IOptionSourceCreateLinks links
            ? Ok((await links.GetCreateLinksAsync(qualifier, HttpContext.RequestAborted)).ToArray())
            : Ok(Array.Empty<OptionCreateLink>());
    }

    /// <summary>
    /// Adds a record by name to the attachment's source (a tag to a taxonomy), when the attachment
    /// allows it and the source supports it. The attachment's static filters say where the record
    /// goes; the source authorizes the caller.
    /// </summary>
    public async Task<IResult> CreateAsync(OptionSourceCreateRequest request)
    {
        if (!await CanViewAsync())
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.DisplayText))
        {
            return ApiProblems.Validation(nameof(OptionSourceCreateRequest.DisplayText), "A name is required.");
        }

        var settings = await GetFieldSettingsAsync(request.ContentType, request.FieldName);
        if (settings is null)
        {
            return NotFound();
        }

        var (providerKey, qualifier) = OptionSourceKeys.Split(settings.SourceKey);
        if (!settings.AllowCreate || FindProvider(providerKey) is not IOptionSourceInlineCreate creator)
        {
            return Problem("This field does not add records to its source.", statusCode: StatusCodes.Status400BadRequest);
        }

        // Only the static filters: they are what places the new record (the taxonomy).
        var filters = settings.Filters.Where(filter => !filter.IsDependent && filter.Value is not null)
            .Select(filter => ResolvedOptionFilter.From(filter, [filter.Value!]))
            .ToArray();
        var result = await creator.CreateAsync(qualifier, filters, request.DisplayText.Trim(), HttpContext.RequestAborted);
        if (result.Forbidden)
        {
            return Forbid();
        }

        return result.Row is { } row
            ? Ok(row)
            : ApiProblems.Validation(new Dictionary<string, string[]> { [nameof(OptionSourceCreateRequest.DisplayText)] = [.. result.Errors] });
    }

    /// <summary>The columns a source can offer - drives the column picker in the field's
    /// settings editor, so paths are never typed blind.</summary>
    public async Task<IResult> DescribeColumnsAsync(string sourceKey)
    {
        if (!await CanViewAsync())
        {
            return Forbid();
        }

        var (providerKey, qualifier) = OptionSourceKeys.Split(sourceKey);
        var provider = FindProvider(providerKey);
        if (provider is null)
        {
            return NotFound();
        }

        return Ok((await provider.DescribeColumnsAsync(qualifier, HttpContext.RequestAborted)).ToArray());
    }

    /// <summary>The registered sources a field can be bound to.</summary>
    public async Task<IResult> ListSourcesAsync()
    {
        if (!await CanViewAsync())
        {
            return Forbid();
        }

        return Ok(sourceProviders.Select(provider => provider.Key).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToArray());
    }

    private IOptionSourceProvider? FindProvider(string providerKey) =>
        sourceProviders.FirstOrDefault(candidate => string.Equals(candidate.Key, providerKey, StringComparison.OrdinalIgnoreCase));

    private async Task<OptionFilterResolution> ResolveFiltersAsync(
        string? contentType,
        string? fieldName,
        IReadOnlyDictionary<string, string[]>? editorState)
    {
        var settings = await GetFieldSettingsAsync(contentType, fieldName);
        return settings is null
            ? OptionFilterResolution.Unrestricted
            : await ResolveTranslatedAsync(contentType!, settings, editorState);
    }

    // Dependent filters compare machine data, but a picker parent's editor state carries stored
    // ids - translate id lists into the configured parent column (Key by default) through the
    // parent's own source before resolving.
    private async Task<OptionFilterResolution> ResolveTranslatedAsync(
        string contentType,
        OptionListFieldSettings settings,
        IReadOnlyDictionary<string, string[]>? editorState)
    {
        var (filters, state) = await OptionDependentFilterTranslator.TranslateAsync(
            settings.Filters,
            ToEditorState(editorState),
            (path, column, raw) => parentValues.ResolveAsync(contentType, path, column, raw, HttpContext.RequestAborted));

        return OptionFilterResolver.Resolve(filters, state);
    }

    private async Task<OptionListFieldSettings?> GetFieldSettingsAsync(string? contentType, string? fieldName)
    {
        if (string.IsNullOrWhiteSpace(contentType) || string.IsNullOrWhiteSpace(fieldName))
        {
            return null;
        }

        var partDefinition = await contentDefinitionManager.GetFieldSetDefinitionAsync(contentType);
        var field = partDefinition?.Fields.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, fieldName, StringComparison.OrdinalIgnoreCase));

        return field?.GetSettings<OptionListFieldSettings>();
    }

    private static bool IsFieldRequired(ContentFieldSetDefinition? partDefinition, string path)
    {
        // Dependency paths are "FieldName" within the same part, or "FieldSet.Field".
        var fieldName = path.Contains('.') ? path[(path.LastIndexOf('.') + 1)..] : path;
        var field = partDefinition?.Fields.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, fieldName, StringComparison.OrdinalIgnoreCase));

        // Read through the typed accessor: a hand-written settings key that does not exist reads
        // as false forever, which would make the derived-required rule silently never fire.
        return field?.GetSettings<OptionListFieldSettings>()?.Required ?? false;
    }

    private static IReadOnlyList<string> FilterPaths(OptionListFieldSettings? settings) =>
        settings is null ? [] : [.. settings.Filters.Select(filter => filter.Path).Distinct(StringComparer.OrdinalIgnoreCase)];

    private static IReadOnlyDictionary<string, IReadOnlyList<string>>? ToEditorState(IReadOnlyDictionary<string, string[]>? state) =>
        state?.ToDictionary(entry => entry.Key, entry => (IReadOnlyList<string>)entry.Value, StringComparer.OrdinalIgnoreCase);

    private async Task<bool> CanViewAsync() =>
        await authorizationService.AuthorizeAsync(User, OptionPickerPermissions.QueryOptionSources);
}
