#nullable enable

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Nodes;
using Grove;
using Grove.ContentManagement;
using Grove.ContentManagement.Records;
using Grove.Contents;
using Grove.Security.Permissions;
using YesSql;

namespace Grove.Contents.Api;

public sealed record ContentItemListResult(ContentItem[] Items, int Total, int Page, int PageSize);

/// <summary>One stored version of a content item (the editor's versions list).</summary>
public sealed record ContentItemVersion(string ContentItemVersionId, bool Published, bool Latest, string? DisplayText, string? Author, DateTime? ModifiedUtc, DateTime? PublishedUtc);

public sealed record ContentItemWriteRequest(string ContentType, string? DisplayText, JsonObject? Content, bool Publish);

public sealed record ContentItem(
    string ContentItemId,
    string ContentItemVersionId,
    string ContentType,
    string DisplayText,
    bool Published,
    bool Latest,
    DateTime? CreatedUtc,
    DateTime? ModifiedUtc,
    DateTime? PublishedUtc,
    string Owner,
    string Author,
    object Content)
{
    public static ContentItem From(Grove.ContentManagement.ContentItem source) => new(
        source.ContentItemId,
        source.ContentItemVersionId,
        source.ContentType,
        source.DisplayText,
        source.Published,
        source.Latest,
        source.CreatedUtc,
        source.ModifiedUtc,
        source.PublishedUtc,
        source.Owner,
        source.Author,
        source.Content);
}
