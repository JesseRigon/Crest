using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Contents;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using System.Text.Json.Nodes;
using Crest.Settings;

namespace Crest.ViewModels;

public sealed record ContentType(
    string Name,
    string DisplayName,
    JsonObject Settings,
    ContentTypePart[] Parts)
{
    public static ContentType From(ContentTypeDefinition source) => new(
        source.Name,
        source.DisplayName,
        source.Settings,
        source.Parts.Select(ContentTypePart.From).ToArray());
}

/// <summary>Lock is the attachment's own lock (docs/Content-Items.md › Definition locks): None, Tenant or Module.</summary>
public sealed record ContentTypePart(
    string Name,
    JsonObject Settings,
    ContentPart Part,
    string Lock)
{
    public static ContentTypePart From(ContentTypePartDefinition source) => new(
        source.Name,
        source.Settings,
        ContentPart.From(source.PartDefinition),
        source.GetLock());
}

/// <summary>Lock is the part definition's own lock; every field reports its effective lock (its own or the part's).</summary>
public sealed record ContentPart(
    string Name,
    JsonObject Settings,
    ContentPartField[] Fields,
    string Lock)
{
    public static ContentPart From(ContentPartDefinition source) => new(
        source.Name,
        source.Settings,
        source.Fields.Select(field => ContentPartField.From(field, source)).ToArray(),
        source.GetLock());
}

public sealed record ContentPartField(
    string Name,
    JsonObject Settings,
    ContentField Field,
    string Lock)
{
    public static ContentPartField From(ContentPartFieldDefinition source, ContentPartDefinition part) => new(
        source.Name,
        source.Settings,
        new ContentField(source.FieldDefinition.Name),
        source.GetEffectiveLock(part));
}

public sealed record ContentField(string Name);
