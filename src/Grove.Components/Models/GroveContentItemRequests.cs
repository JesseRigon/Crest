using System.Text.Json.Nodes;

namespace Grove.Components.Models;

// These mirror the server's ContentItemWriteRequest (api/grove/content-items): both
// the create (POST) and update (PUT) actions bind the SAME record, so ContentType is
// required on updates too, and Content must be a JsonObject - a bare JsonNode fails
// model binding with a 400.
public sealed record UpdateGroveContentItemRequest(
    string ContentType,
    string? DisplayText,
    JsonObject? Content = null,
    bool Publish = false);

public sealed record CreateGroveContentItemRequest(
    string ContentType,
    string? DisplayText,
    JsonObject? Content = null,
    bool Publish = false);

public sealed record GroveAntiforgeryToken(
    string HeaderName,
    string RequestToken);
