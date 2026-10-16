using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grove.Data.Connectors;

namespace Grove.Data.Protocols.Http;

/// <summary>Resolves a relative path under a base URL and refuses one that would leave it.</summary>
public static class BaseUrlPath
{
    public static bool TryResolve(string? baseUrl, string? path, out Uri uri, out string error)
    {
        uri = null!;
        error = string.Empty;
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            error = "The connection has no http or https base URL.";
            return false;
        }

        path = path?.Trim() ?? string.Empty;
        if (path.Contains("://", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal) || path.StartsWith('\\') || path.Contains('\\'))
        {
            error = "The path must be relative to the connection's base URL.";
            return false;
        }

        var root = baseUri.AbsoluteUri.EndsWith('/') ? baseUri : new Uri(baseUri.AbsoluteUri + "/");
        uri = new Uri(root, path.TrimStart('/'));
        if (uri.Scheme != root.Scheme || !string.Equals(uri.Authority, root.Authority, StringComparison.OrdinalIgnoreCase)
            || !uri.AbsolutePath.StartsWith(root.AbsolutePath, StringComparison.Ordinal))
        {
            error = "The path leaves the connection's base URL.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// A next-page link the service returned, as a path relative to the base URL; null when it
    /// points elsewhere.
    /// </summary>
    public static string? Relativize(string? baseUrl, string link)
    {
        if (!Uri.TryCreate(link, UriKind.Absolute, out var absolute))
        {
            return link;
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            return null;
        }

        var root = baseUri.AbsoluteUri.EndsWith('/') ? baseUri.AbsoluteUri : baseUri.AbsoluteUri + "/";
        return absolute.AbsoluteUri.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? absolute.AbsoluteUri[root.Length..] : null;
    }
}

/// <summary>Builds an HTTP request from an operation and its arguments, and reads the response.</summary>
public static class HttpComposer
{
    /// <summary>The argument that carries a raw body (JSON value or text).</summary>
    public const string BodyArgument = "body";

    /// <summary>
    /// Composes the request: the operation's method and path template filled from the
    /// arguments (path parameters escaped), query and header parameters, the body; a raw
    /// <see cref="IProtocolCallContext.Request"/> supplies the method when there is no
    /// operation, and overrides path, headers, content type and body.
    /// </summary>
    public static HttpRequestMessage Compose(IProtocolCallContext context)
    {
        var operation = context.Operation;
        var raw = context.Request;
        var arguments = context.Arguments ?? [];
        // An operation with a request override: the operation decides the method (and so its
        // safety and idempotency header); the override supplies the path, headers and body.
        var method = operation?.Method ?? raw?.Method ?? "GET";
        var path = raw?.Path ?? operation?.Path ?? string.Empty;
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        path = FillTemplate(path, arguments, used);
        var query = new List<string>();
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in operation?.Parameters ?? [])
        {
            if (parameter.In == ParameterLocation.Path || !arguments.TryGetPropertyValue(parameter.Name, out var value) || value is null)
            {
                continue;
            }

            used.Add(parameter.Name);
            switch (parameter.In)
            {
                case ParameterLocation.Query:
                    if (value is JsonArray array)
                    {
                        query.AddRange(array.Select(item => $"{Uri.EscapeDataString(parameter.Name)}={Uri.EscapeDataString(Text(item))}"));
                    }
                    else
                    {
                        query.Add($"{Uri.EscapeDataString(parameter.Name)}={Uri.EscapeDataString(Text(value))}");
                    }

                    break;
                case ParameterLocation.Header:
                    headers[parameter.Name] = Text(value);
                    break;
                case ParameterLocation.Body:
                    used.Remove(parameter.Name);
                    break;
            }
        }

        if (context.Page is { Length: > 0 } page && operation?.Paging is { } paging)
        {
            switch (paging.Style)
            {
                case PagingStyle.Link:
                    path = page;
                    query.Clear();
                    break;
                case PagingStyle.Cursor or PagingStyle.Page or PagingStyle.Offset when paging.Parameter is { } name:
                    query.RemoveAll(q => q.StartsWith(Uri.EscapeDataString(name) + "=", StringComparison.Ordinal));
                    query.Add($"{Uri.EscapeDataString(name)}={Uri.EscapeDataString(page)}");
                    break;
            }
        }

        if (query.Count > 0)
        {
            path += (path.Contains('?') ? "&" : "?") + string.Join('&', query);
        }

        var message = new HttpRequestMessage(new HttpMethod(method.ToUpperInvariant()), context.ResolveUri(path));
        foreach (var (name, value) in headers)
        {
            message.Headers.TryAddWithoutValidation(name, value);
        }

        foreach (var (name, value) in raw?.Headers ?? new Dictionary<string, string>())
        {
            if (!string.Equals(name, "Host", StringComparison.OrdinalIgnoreCase) && !string.Equals(name, "Authorization", StringComparison.OrdinalIgnoreCase))
            {
                message.Headers.Remove(name);
                message.Headers.TryAddWithoutValidation(name, value);
            }
        }

        if (context.IdempotencyKey is { Length: > 0 } key)
        {
            var header = operation?.IdempotencyKeyHeader ?? "Idempotency-Key";
            if (operation is null || operation.SupportsIdempotencyKey || raw is not null)
            {
                message.Headers.Remove(header);
                message.Headers.TryAddWithoutValidation(header, key);
            }
        }

        if (message.Method != HttpMethod.Get && message.Method != HttpMethod.Head)
        {
            var contentType = raw?.ContentType ?? operation?.ContentType ?? "application/json";
            string? body = raw?.Body;
            if (body is null)
            {
                if (arguments.TryGetPropertyValue(BodyArgument, out var bodyNode) && bodyNode is not null)
                {
                    body = bodyNode is JsonValue v && v.TryGetValue<string>(out var text) ? text : bodyNode.ToJsonString();
                }
                else if (operation is not null)
                {
                    // The arguments no parameter took are the body.
                    var rest = new JsonObject();
                    foreach (var (name, value) in arguments)
                    {
                        if (!used.Contains(name))
                        {
                            rest[name] = value?.DeepClone();
                        }
                    }

                    body = rest.Count > 0 ? rest.ToJsonString() : null;
                }
            }

            if (body is not null)
            {
                message.Content = new StringContent(body, Encoding.UTF8);
                message.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            }
        }

        return message;
    }

    /// <summary>Sends a composed request (authorized) and reads it into a <see cref="ProtocolResponse"/>.</summary>
    public static async Task<ProtocolResponse> SendAsync(IProtocolCallContext context, HttpRequestMessage message, CancellationToken cancellationToken)
    {
        await context.AuthorizeAsync(message, cancellationToken);
        using var response = await context.HttpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var body = await context.ReadBodyAsync(response, cancellationToken);
        var status = (int)response.StatusCode;
        var headers = Headers(response);
        var failure = HttpFailures.Classify(status, response.Headers);
        if (status is >= 300 and < 400)
        {
            failure = new FailureClassification(FailureKind.Permanent);
        }

        var value = ParseJson(body, response.Content.Headers.ContentType?.MediaType);
        return new ProtocolResponse
        {
            StatusCode = status,
            Body = body,
            Value = value,
            Headers = headers,
            Failure = failure,
            Error = failure.IsFailure ? (status is >= 300 and < 400 ? $"HTTP {status} from {message.RequestUri?.Host}: redirects are not followed." : $"HTTP {status} from {message.RequestUri?.Host}.") : null,
            NextPage = failure.IsFailure ? null : NextPage(context, response, value),
        };
    }

    public static JsonNode? ParseJson(string? body, string? mediaType)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        var looksJson = mediaType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true || body.TrimStart()[0] is '{' or '[';
        if (!looksJson)
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(body);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? NextPage(IProtocolCallContext context, HttpResponseMessage response, JsonNode? value)
    {
        if (context.Operation?.Paging is not { } paging)
        {
            return null;
        }

        switch (paging.Style)
        {
            case PagingStyle.Link:
                if (response.Headers.TryGetValues("Link", out var links))
                {
                    foreach (var part in links.SelectMany(l => l.Split(',')))
                    {
                        if (part.Contains("rel=\"next\"", StringComparison.OrdinalIgnoreCase) || part.Contains("rel=next", StringComparison.OrdinalIgnoreCase))
                        {
                            var start = part.IndexOf('<');
                            var end = part.IndexOf('>');
                            if (start >= 0 && end > start)
                            {
                                return BaseUrlPath.Relativize(context.Connection.BaseUrl, part[(start + 1)..end]);
                            }
                        }
                    }
                }

                return paging.NextPath is { } linkPath && Select(value, linkPath) is { } link ? BaseUrlPath.Relativize(context.Connection.BaseUrl, link) : null;
            case PagingStyle.Cursor:
                return paging.NextPath is { } cursorPath ? Select(value, cursorPath) : null;
            case PagingStyle.Page:
                return int.TryParse(context.Page, out var number) ? (number + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) : "2";
            default:
                return null;
        }
    }

    /// <summary>A dotted path into a JSON value (<c>paging.next</c>); null when absent or empty.</summary>
    public static string? Select(JsonNode? value, string path)
    {
        var node = value;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            node = node is JsonObject obj && obj.TryGetPropertyValue(segment, out var child) ? child : null;
        }

        var text = node is null ? null : Text(node);
        return string.IsNullOrEmpty(text) ? null : text;
    }

    public static IReadOnlyDictionary<string, string> Headers(HttpResponseMessage response)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, values) in response.Headers.Concat(response.Content.Headers))
        {
            if (!string.Equals(name, "Set-Cookie", StringComparison.OrdinalIgnoreCase))
            {
                headers[name] = string.Join(", ", values);
            }
        }

        return headers;
    }

    private static string FillTemplate(string path, JsonObject arguments, HashSet<string> used)
    {
        var builder = new StringBuilder();
        var i = 0;
        while (i < path.Length)
        {
            var open = path.IndexOf('{', i);
            if (open < 0)
            {
                builder.Append(path, i, path.Length - i);
                break;
            }

            var close = path.IndexOf('}', open);
            if (close < 0)
            {
                builder.Append(path, i, path.Length - i);
                break;
            }

            builder.Append(path, i, open - i);
            var name = path[(open + 1)..close];
            if (!arguments.TryGetPropertyValue(name, out var value) || value is null)
            {
                throw new ConnectorRefusedException($"The path parameter '{name}' has no value.");
            }

            used.Add(name);
            builder.Append(Uri.EscapeDataString(Text(value)));
            i = close + 1;
        }

        return builder.ToString();
    }

    private static string Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : node?.ToJsonString() ?? string.Empty;
}
