#nullable enable

using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Grove.Api;
using Grove.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.Contents;
using Grove.Contents.Management;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents.Management.Metadata.Records;
using Grove.Contents.Management.Metadata.Settings;
using System.Text.Json.Nodes;
using Grove.Infrastructure.Settings;

namespace Grove.Contents.Types.Api;

public sealed class ContentTypesEditorApi : ApiContributor
{
    public override string Name => "ContentTypesEditor";

    public override string FeatureId => "Grove.Contents.Types";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("content-types");
        routes.MapPut("{contentType}/menu", (HttpContext __http, [FromRoute] string contentType, [FromBody] SetContentTypeMenuRequest request) => ApiHandlers.For<ContentTypesHandlers>(__http).SetMenu(contentType, request));
        routes.MapGet("field-types", ApiHandlers.Handle<ContentTypesHandlers>(h => h.ListFieldTypes()));
        routes.MapPut("fields", (HttpContext __http, [FromBody] SetFieldTypeRequest request) => ApiHandlers.For<ContentTypesHandlers>(__http).SetFieldType(request));
        routes.MapGet("", ApiHandlers.Handle<ContentTypesHandlers>(h => h.List()));
        routes.MapGet("field-sets", ApiHandlers.Handle<ContentTypesHandlers>(h => h.ListFieldSets()));
        routes.MapPut("{contentType}/field-sets", (HttpContext __http, [FromRoute] string contentType, [FromBody] AttachFieldSetRequest request) => ApiHandlers.For<ContentTypesHandlers>(__http).AttachFieldSet(contentType, request));
        routes.MapDelete("{contentType}/field-sets/{fieldSetName}", (HttpContext __http, [FromRoute] string contentType, [FromRoute] string fieldSetName) => ApiHandlers.For<ContentTypesHandlers>(__http).DetachFieldSet(contentType, fieldSetName));
        routes.MapPut("locks", (HttpContext __http, [FromBody] SetDefinitionLockRequest request) => ApiHandlers.For<ContentTypesHandlers>(__http).SetLock(request));
        routes.MapPut("{contentType}/embeddings", (HttpContext __http, [FromRoute] string contentType, [FromBody] EmbeddingRequest request) => ApiHandlers.For<ContentTypesHandlers>(__http).PutEmbedding(contentType, request));
        routes.MapDelete("{contentType}/embeddings/{embedding}", (HttpContext __http, [FromRoute] string contentType, [FromRoute] string embedding) => ApiHandlers.For<ContentTypesHandlers>(__http).RemoveEmbedding(contentType, embedding));
        routes.MapPut("{contentType}/embeddings/{embedding}/lock", (HttpContext __http, [FromRoute] string contentType, [FromRoute] string embedding, [FromBody] EmbeddingLockRequest request) => ApiHandlers.For<ContentTypesHandlers>(__http).SetEmbeddingLock(contentType, embedding, request));
        routes.MapGet("{contentType}", (HttpContext __http, [FromRoute] string contentType) => ApiHandlers.For<ContentTypesHandlers>(__http).Get(contentType));
    }
}

public sealed class ContentTypesHandlers(
    IContentDefinitionManager contentDefinitionManager,
    IOptions<ContentOptions> contentOptions,
    IOptions<Grove.Contents.Fields.ContentFieldTypeOptions> fieldTypes,
    IAuthorizationService authorization,
    DefinitionLockGuard locks) : ApiHandlers
{
    /// <summary>
    /// Sets whether the type gets its own entry under the Content menu (the
    /// generic content-items page filtered to it). Runs the provider-menu sync
    /// afterwards: Grove's menu is materialized into the admin menu document
    /// rather than rebuilt per request, and the automatic sync only runs once
    /// per shell — without this the toggle would not surface until a recycle.
    /// The sync is DEFERRED, not inline: the alter is only visible once this
    /// scope's session commits, so an inline sync would read the pre-change
    /// definitions and import the old state (verified live, both directions).
    /// </summary>
    public async Task<IResult> SetMenu(string contentType, [FromBody] SetContentTypeMenuRequest request)
    {
        if (!await authorization.AuthorizeAsync(User, ContentTypesPermissions.EditContentTypes)) return Forbid();

        var typeDefinition = await contentDefinitionManager.GetTypeDefinitionAsync(contentType);
        if (typeDefinition is null)
        {
            return NotFound();
        }

        await contentDefinitionManager.AlterTypeDefinitionAsync(contentType, type => type
            .MergeSettings<GroveContentTypeMenuSettings>(settings =>
                settings.ShowInContentMenu = request.ShowInContentMenu));

        return NoContent();
    }

    /// <summary>The registered content field types a field can be - what the
    /// definition screens offer in their field-type dropdown.</summary>
    public async Task<IResult> ListFieldTypes()
    {
        if (!await authorization.AuthorizeAsync(User, ContentTypesPermissions.ViewContentTypes)) return Forbid();

        // Exactly the field types registered with the field type registry (docs/content-engine.md › Fields and field types).
        return Ok(fieldTypes.Value.Types
            .Select(type => type.Name)
            .Order(StringComparer.Ordinal)
            .ToArray());
    }

    /// <summary>
    /// Sets a field's TYPE (creating the field when absent). Conversion removes and
    /// re-adds the field definition, keeping its position and display name; stored
    /// content is untouched - each item's data stays in its own document, which is
    /// what lets readers fall back to the previous shape. Converting TO an option
    /// picker normally goes through the picker settings editor instead, so the
    /// binding is configured in the same step.
    /// </summary>
    public async Task<IResult> SetFieldType([FromBody] SetFieldTypeRequest request)
    {
        if (!await authorization.AuthorizeAsync(User, ContentTypesPermissions.EditContentTypes)) return Forbid();

        if (!fieldTypes.Value.IsRegistered(request.FieldType ?? string.Empty))
        {
            return Problem($"'{request.FieldType}' is not a registered field type.", statusCode: StatusCodes.Status400BadRequest);
        }

        var existing = await contentDefinitionManager.GetFieldSetDefinitionAsync(request.FieldSet);
        var current = existing?.Fields.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, request.Field, StringComparison.OrdinalIgnoreCase));

        // A field set defined in code stores its properties beside its fields in one JSON
        // object, so a new field cannot take a property's name.
        if (current is null &&
            contentOptions.Value.ContentFieldSetOptionsLookup.TryGetValue(request.FieldSet, out var codeFieldSet) &&
            codeFieldSet.Type.GetProperty(request.Field, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase) is not null)
        {
            return Problem($"The field set '{request.FieldSet}' stores a value named '{request.Field}'; the field needs another name.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (string.Equals(current?.FieldTypeDefinition?.Name, request.FieldType, StringComparison.Ordinal))
        {
            return NoContent();
        }

        // A locked field keeps its type (docs/content-engine.md › Definition locks); 409 via the lock filter.
        if (current is not null)
        {
            await locks.EnsureFieldChangeAsync(request.FieldSet, request.Field, DefinitionChanges.Retyped);
        }

        var position = current?.Settings?["ContentFieldSettings"]?["Position"]?.ToString();
        var displayName = request.DisplayName
            ?? current?.Settings?["ContentFieldSettings"]?["DisplayName"]?.ToString()
            ?? request.Field;

        // The remove/re-add dance would otherwise DESTROY every custom settings
        // section on the field (visibility conditions, lock designations, picker
        // settings). Carry them across the conversion: harmless when the new type
        // ignores a section, and it restores intact if the type converts back.
        // ContentFieldSettings is rebuilt from position/displayName above.
        var preserved = current?.Settings?.DeepClone()?.AsObject();
        preserved?.Remove("ContentFieldSettings");

        if (current is not null)
        {
            await contentDefinitionManager.AlterFieldSetDefinitionAsync(request.FieldSet, part => part.RemoveField(request.Field));
        }

        await contentDefinitionManager.AlterFieldSetDefinitionAsync(request.FieldSet, part => part
            .WithField(request.Field, field =>
            {
                field.OfType(request.FieldType).WithDisplayName(displayName);
                if (!string.IsNullOrWhiteSpace(position))
                {
                    field.WithPosition(position);
                }

                if (preserved is { Count: > 0 })
                {
                    field.MergeSettings(preserved);
                }
            }));

        return NoContent();
    }

    public async Task<IResult> List()
    {
        if (!await authorization.AuthorizeAsync(User, ContentTypesPermissions.ViewContentTypes)) return Forbid();
        var definitions = await contentDefinitionManager.ListTypeDefinitionsAsync();
        return Ok(definitions.Select(ContentType.From).ToArray());
    }

    /// <summary>
    /// Every part DEFINITION, attached to a type or not, with its fields and the
    /// types using it. The Field groups screen edits fields HERE because a part is
    /// shared: a field change lands on every type carrying the part.
    /// </summary>
    public async Task<IResult> ListFieldSets()
    {
        if (!await authorization.AuthorizeAsync(User, ContentTypesPermissions.ViewContentTypes)) return Forbid();

        var types = (await contentDefinitionManager.ListTypeDefinitionsAsync()).ToArray();
        var usage = types
            .SelectMany(type => type.FieldSets.Select(part => (Part: part.FieldSetDefinition.Name, Type: type.Name)))
            .GroupBy(entry => entry.Part, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Select(entry => entry.Type).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray(), StringComparer.OrdinalIgnoreCase);

        var parts = await contentDefinitionManager.ListFieldSetDefinitionsAsync();
        return Ok(parts
            .Select(part => new ContentFieldSetDefinitionModel(
                part.Name,
                ContentFieldSet.From(part),
                usage.TryGetValue(part.Name, out var usedBy) ? usedBy : []))
            .OrderBy(part => part.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray());
    }

    /// <summary>
    /// Attaches a part to a type, or updates the ATTACHMENT's instance settings
    /// (display name, description, position) when it is already attached. The part's
    /// own fields are not touched here - those belong to the part definition and are
    /// edited on the Field groups screen, because they are shared across every type
    /// carrying the part.
    /// </summary>
    public async Task<IResult> AttachFieldSet(string contentType, [FromBody] AttachFieldSetRequest request)
    {
        if (!await authorization.AuthorizeAsync(User, ContentTypesPermissions.EditContentTypes)) return Forbid();

        var typeDefinition = await contentDefinitionManager.GetTypeDefinitionAsync(contentType);
        if (typeDefinition is null)
        {
            return NotFound();
        }

        // The part must exist as a definition (or be the type's own implicit part) -
        // silently creating an empty definition from a typo helps no one.
        var partDefinition = await contentDefinitionManager.GetFieldSetDefinitionAsync(request.FieldSet);
        if (partDefinition is null && !string.Equals(request.FieldSet, contentType, StringComparison.OrdinalIgnoreCase))
        {
            return Problem($"There is no field set named '{request.FieldSet}'.", statusCode: StatusCodes.Status400BadRequest);
        }

        await contentDefinitionManager.AlterTypeDefinitionAsync(contentType, type => type
            .WithFieldSet(request.FieldSet, part =>
            {
                if (request.DisplayName is not null)
                {
                    part.WithDisplayName(request.DisplayName);
                }

                if (request.Description is not null)
                {
                    part.WithDescription(request.Description);
                }

                if (!string.IsNullOrWhiteSpace(request.Position))
                {
                    part.WithPosition(request.Position);
                }
            }));

        return NoContent();
    }

    /// <summary>Detaches a part from a type. The part definition and its fields
    /// survive - other types may carry it, and stored content stays in each item's
    /// document.</summary>
    public async Task<IResult> DetachFieldSet(string contentType, string fieldSetName)
    {
        if (!await authorization.AuthorizeAsync(User, ContentTypesPermissions.EditContentTypes)) return Forbid();

        var typeDefinition = await contentDefinitionManager.GetTypeDefinitionAsync(contentType);
        var attached = typeDefinition?.FieldSets.FirstOrDefault(part =>
            string.Equals(part.Name, fieldSetName, StringComparison.OrdinalIgnoreCase));
        if (attached is null)
        {
            return NotFound();
        }

        await locks.EnsureTypeFieldSetChangeAsync(contentType, fieldSetName, DefinitionChanges.Detached);
        await contentDefinitionManager.AlterTypeDefinitionAsync(contentType, type => type.RemoveFieldSet(fieldSetName));
        return NoContent();
    }

    /// <summary>
    /// Places or lifts a TENANT lock on a part definition (Part), one of its fields (Part +
    /// Field) or a part's attachment to a type (Type + Part). Module locks never move here;
    /// the permission is LockContentDefinitions, not EditContentTypes (docs/content-engine.md
    /// › Definition locks).
    /// </summary>
    public async Task<IResult> SetLock([FromBody] SetDefinitionLockRequest request)
    {
        if (!await authorization.AuthorizeAsync(User, ContentDefinitionPermissions.LockContentDefinitions)) return Forbid();

        var requested = DefinitionLockSources.Normalize(request.Lock);

        if (!string.IsNullOrWhiteSpace(request.Type))
        {
            var type = await contentDefinitionManager.LoadTypeDefinitionAsync(request.Type);
            var typeFieldSet = type?.FieldSets.FirstOrDefault(candidate => string.Equals(candidate.Name, request.FieldSet, StringComparison.OrdinalIgnoreCase));
            if (typeFieldSet is null)
            {
                return NotFound();
            }

            DefinitionLockGuard.EnsureLockChange(typeFieldSet.GetLock(), requested, $"Field set '{request.FieldSet}' on type '{request.Type}'");
            await contentDefinitionManager.AlterTypeDefinitionAsync(request.Type, type => type.WithFieldSet(request.FieldSet, part => part.Locked(requested)));
            return NoContent();
        }

        var partDefinition = await contentDefinitionManager.LoadFieldSetDefinitionAsync(request.FieldSet);
        if (partDefinition is null)
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(request.Field))
        {
            var field = partDefinition.Fields.FirstOrDefault(candidate => string.Equals(candidate.Name, request.Field, StringComparison.OrdinalIgnoreCase));
            if (field is null)
            {
                return NotFound();
            }

            DefinitionLockGuard.EnsureLockChange(field.GetLock(), requested, $"Field '{request.FieldSet}.{request.Field}'");
            await contentDefinitionManager.AlterFieldSetDefinitionAsync(request.FieldSet, part => part.WithField(request.Field, builder => builder.Locked(requested)));
            return NoContent();
        }

        DefinitionLockGuard.EnsureLockChange(partDefinition.GetLock(), requested, $"Field set '{request.FieldSet}'");
        await contentDefinitionManager.AlterFieldSetDefinitionAsync(request.FieldSet, part => part.Locked(requested));
        return NoContent();
    }

    public async Task<IResult> Get(string contentType)
    {
        if (!await authorization.AuthorizeAsync(User, ContentTypesPermissions.ViewContentTypes)) return Forbid();
        var definition = await contentDefinitionManager.GetTypeDefinitionAsync(contentType);
        return definition is null ? NotFound() : Ok(ContentType.From(definition));
    }

    /// <summary>
    /// Declares or changes an embedding on a type (docs/content-engine.md › Embedded content
    /// items). A locked embedding takes only display changes (display name, description,
    /// position); anything else answers 409.
    /// </summary>
    public async Task<IResult> PutEmbedding(string contentType, [FromBody] EmbeddingRequest request)
    {
        if (!await authorization.AuthorizeAsync(User, ContentTypesPermissions.EditContentTypes)) return Forbid();

        var type = await contentDefinitionManager.LoadTypeDefinitionAsync(contentType);
        if (type is null)
        {
            return NotFound();
        }

        var types = (await contentDefinitionManager.ListTypeDefinitionsAsync()).ToList();
        var problems = EmbeddingProblems(type, request, types);
        if (problems.Count > 0)
        {
            return Problem(string.Join(" ", problems), statusCode: StatusCodes.Status400BadRequest);
        }

        var record = request.ToRecord();
        var existing = type.Embeddings.FirstOrDefault(candidate => string.Equals(candidate.Name, request.Name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null && !SameContract(existing.ToRecord(), record))
        {
            await locks.EnsureEmbeddingChangeAsync(contentType, existing.Name, DefinitionChanges.SettingsChanged);
        }

        await contentDefinitionManager.AlterTypeDefinitionAsync(contentType, builder => builder
            .WithEmbedding(existing?.Name ?? request.Name.Trim(), embedding => embedding.From(record)));

        return NoContent();
    }

    public async Task<IResult> RemoveEmbedding(string contentType, string embedding)
    {
        if (!await authorization.AuthorizeAsync(User, ContentTypesPermissions.EditContentTypes)) return Forbid();

        var type = await contentDefinitionManager.LoadTypeDefinitionAsync(contentType);
        if (type?.Embeddings.Any(candidate => string.Equals(candidate.Name, embedding, StringComparison.OrdinalIgnoreCase)) != true)
        {
            return NotFound();
        }

        await locks.EnsureEmbeddingChangeAsync(contentType, embedding, DefinitionChanges.Removed);
        await contentDefinitionManager.AlterTypeDefinitionAsync(contentType, builder => builder.RemoveEmbedding(embedding));
        return NoContent();
    }

    /// <summary>Places or lifts a tenant lock on an embedding; a Module lock never moves here.</summary>
    public async Task<IResult> SetEmbeddingLock(string contentType, string embedding, [FromBody] EmbeddingLockRequest request)
    {
        if (!await authorization.AuthorizeAsync(User, ContentDefinitionPermissions.LockContentDefinitions)) return Forbid();

        var type = await contentDefinitionManager.LoadTypeDefinitionAsync(contentType);
        var existing = type?.Embeddings.FirstOrDefault(candidate => string.Equals(candidate.Name, embedding, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            return NotFound();
        }

        var requested = DefinitionLockSources.Normalize(request.Lock);
        DefinitionLockGuard.EnsureLockChange(existing.GetLock(), requested, $"Embedding '{embedding}' on type '{contentType}'");
        await contentDefinitionManager.AlterTypeDefinitionAsync(contentType, builder => builder.WithEmbedding(existing.Name, e => e.Locked(requested)));
        return NoContent();
    }

    // Everything but the display surface.
    private static bool SameContract(EmbeddedContentDefinitionRecord current, EmbeddedContentDefinitionRecord requested) =>
        current.AllowedTypes.SequenceEqual(requested.AllowedTypes, StringComparer.OrdinalIgnoreCase)
        && current.MinCount == requested.MinCount
        && current.MaxCount == requested.MaxCount
        && current.Ordered == requested.Ordered
        && string.Equals(current.DisplayType, requested.DisplayType, StringComparison.Ordinal)
        && current.Nested == requested.Nested
        && current.Collapsed == requested.Collapsed
        && current.Referenceable == requested.Referenceable
        && current.Mode == requested.Mode
        && current.CopiedValues.SequenceEqual(requested.CopiedValues, StringComparer.Ordinal);

    /// <summary>What is wrong with an embedding declaration on <paramref name="type"/>.</summary>
    public static List<string> EmbeddingProblems(ContentTypeDefinition type, EmbeddingRequest request, IReadOnlyList<ContentTypeDefinition> types)
    {
        var problems = new List<string>();
        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length == 0 || !char.IsLetter(name[0]) || !name.All(char.IsLetterOrDigit))
        {
            problems.Add("An embedding name starts with a letter and holds only letters and digits.");
        }

        if (type.FieldSets.Any(fieldSet => string.Equals(fieldSet.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            problems.Add($"'{name}' is the name of a field group on the type.");
        }

        var allowed = request.AllowedTypes ?? [];
        if (allowed.Length == 0)
        {
            problems.Add("An embedding allows at least one content type or stereotype.");
        }

        foreach (var entry in allowed.Where(entry => !entry.StartsWith('@') && entry != EmbeddedContentDefinition.AnyType))
        {
            if (!types.Any(candidate => string.Equals(candidate.Name, entry, StringComparison.OrdinalIgnoreCase)))
            {
                problems.Add($"There is no content type named '{entry}'.");
            }
        }

        if (request.MinCount < 0 || request.MaxCount < 0 || (request.MinCount is { } min && request.MaxCount is { } max && min > max))
        {
            problems.Add("The count limits are not a range.");
        }

        if (!EmbeddingModes.All.Contains(request.Mode ?? EmbeddingModes.Live, StringComparer.OrdinalIgnoreCase))
        {
            problems.Add($"'{request.Mode}' is not one of {string.Join(", ", EmbeddingModes.All)}.");
        }

        // Embedding goes a few levels deep and never back into itself.
        // An embedding allowing any type leaves the type to its parent (a taxonomy's term type).
        var probe = new EmbeddedContentDefinition(request.ToRecord());
        foreach (var allowedType in allowed.Contains(EmbeddedContentDefinition.AnyType) ? [] : types.Where(probe.Allows))
        {
            if (Depth(allowedType, types, [type.Name]) is not { } depth || depth + 1 > EmbeddingDepth.Max)
            {
                problems.Add($"Embedding '{allowedType.Name}' here nests too deep or back into '{type.Name}'.");
            }
        }

        return problems;
    }

    // How many levels of embeddings a type carries; null when it reaches back into the path.
    private static int? Depth(ContentTypeDefinition type, IReadOnlyList<ContentTypeDefinition> types, HashSet<string> path)
    {
        if (!path.Add(type.Name))
        {
            return null;
        }

        var deepest = 0;
        foreach (var allowed in type.Embeddings.SelectMany(embedding => types.Where(embedding.Allows)).DistinctBy(candidate => candidate.Name))
        {
            if (Depth(allowed, types, [.. path]) is not { } depth)
            {
                return null;
            }

            deepest = Math.Max(deepest, depth + 1);
        }

        return deepest;
    }
}

/// <summary>An embedding declaration (docs/content-engine.md › Embedded content items).</summary>
public sealed record EmbeddingRequest(
    [property: NotEmpty] string Name,
    string? DisplayName = null,
    string? Description = null,
    string? Position = null,
    string[]? AllowedTypes = null,
    int? MinCount = null,
    int? MaxCount = null,
    bool Ordered = true,
    string? DisplayType = null,
    bool Nested = false,
    bool Collapsed = false,
    bool Referenceable = false,
    string? Mode = null,
    string[]? CopiedValues = null)
{
    public EmbeddedContentDefinitionRecord ToRecord() => new()
    {
        Name = Name?.Trim(),
        DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? null : DisplayName.Trim(),
        Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
        Position = string.IsNullOrWhiteSpace(Position) ? null : Position.Trim(),
        AllowedTypes = [.. (AllowedTypes ?? []).Where(entry => !string.IsNullOrWhiteSpace(entry)).Select(entry => entry.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)],
        MinCount = MinCount,
        MaxCount = MaxCount,
        Ordered = Ordered,
        DisplayType = string.IsNullOrWhiteSpace(DisplayType) ? null : DisplayType.Trim(),
        Nested = Nested,
        Collapsed = Collapsed,
        Referenceable = Referenceable,
        Mode = EmbeddingModes.Normalize(Mode),
        CopiedValues = [.. (CopiedValues ?? []).Where(entry => !string.IsNullOrWhiteSpace(entry)).Select(entry => entry.Trim())],
    };
}

public sealed record EmbeddingLockRequest(string Lock = DefinitionLockSources.None);

/// <summary>Content-menu designation payload for a type.</summary>
public sealed record SetContentTypeMenuRequest(bool ShowInContentMenu);

/// <summary>Field-type change payload. Part is the part DEFINITION name; a field
/// that does not exist yet is created.</summary>
public sealed record SetFieldTypeRequest([property: NotEmpty] string FieldSet, [property: NotEmpty] string Field, [property: NotEmpty] string FieldType, string? DisplayName = null);

/// <summary>A part DEFINITION with its fields and the types carrying it.</summary>
public sealed record ContentFieldSetDefinitionModel(string Name, ContentFieldSet FieldSet, string[] UsedByTypes);

/// <summary>Attach a part to a type / update the attachment's instance settings.
/// Null leaves a setting unchanged.</summary>
public sealed record AttachFieldSetRequest([property: NotEmpty] string FieldSet, string? DisplayName = null, string? Description = null, string? Position = null);

/// <summary>A tenant lock change: Part alone, Part + Field, or Type + Part. Lock is None or Tenant.</summary>
public sealed record SetDefinitionLockRequest([property: NotEmpty] string FieldSet, string? Field = null, string? Type = null, string Lock = DefinitionLockSources.None);
