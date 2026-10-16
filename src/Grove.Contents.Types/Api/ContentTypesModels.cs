#nullable enable

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Grove.Contents;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;
using System.Text.Json.Nodes;
using Grove.Infrastructure.Settings;

namespace Grove.Contents.Types.Api;

public sealed record ContentType(
    string Name,
    string DisplayName,
    JsonObject Settings,
    ContentTypeFieldSet[] FieldSets,
    ContentTypeEmbedding[] Embeddings)
{
    public static ContentType From(ContentTypeDefinition source) => new(
        source.Name,
        source.DisplayName,
        source.Settings,
        source.FieldSets.Select(ContentTypeFieldSet.From).ToArray(),
        source.Embeddings.Select(ContentTypeEmbedding.From).ToArray());
}

/// <summary>An embedding on a type (docs/content-engine.md › Embedded content items); Lock is its own lock.</summary>
public sealed record ContentTypeEmbedding(
    string Name,
    string? DisplayName,
    string? Description,
    string? Position,
    string[] AllowedTypes,
    int? MinCount,
    int? MaxCount,
    bool Ordered,
    string? DisplayType,
    bool Nested,
    bool Collapsed,
    bool Referenceable,
    string Mode,
    string[] CopiedValues,
    string Lock)
{
    public static ContentTypeEmbedding From(EmbeddedContentDefinition source) => new(
        source.Name,
        source.DisplayName,
        source.Description,
        source.Position,
        [.. source.AllowedTypes],
        source.MinCount,
        source.MaxCount,
        source.Ordered,
        source.DisplayType,
        source.Nested,
        source.Collapsed,
        source.Referenceable,
        source.Mode,
        [.. source.CopiedValues],
        source.GetLock());
}

/// <summary>Lock is the attachment's own lock (docs/content-engine.md › Definition locks): None, Tenant or Module.</summary>
public sealed record ContentTypeFieldSet(
    string Name,
    JsonObject Settings,
    ContentFieldSet FieldSet,
    string Lock)
{
    public static ContentTypeFieldSet From(ContentTypeFieldSetDefinition source) => new(
        source.Name,
        source.Settings,
        ContentFieldSet.From(source.FieldSetDefinition),
        source.GetLock());
}

/// <summary>Lock is the part definition's own lock; every field reports its effective lock (its own or the part's).</summary>
public sealed record ContentFieldSet(
    string Name,
    JsonObject Settings,
    ContentField[] Fields,
    string Lock)
{
    public static ContentFieldSet From(ContentFieldSetDefinition source) => new(
        source.Name,
        source.Settings,
        source.Fields.Select(field => ContentField.From(field, source)).ToArray(),
        source.GetLock());
}

public sealed record ContentField(
    string Name,
    JsonObject Settings,
    ContentFieldType FieldType,
    string Lock)
{
    public static ContentField From(ContentFieldDefinition source, ContentFieldSetDefinition part) => new(
        source.Name,
        source.Settings,
        new ContentFieldType(source.FieldTypeDefinition.Name),
        source.GetEffectiveLock(part));
}

public sealed record ContentFieldType(string Name);
