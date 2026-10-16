using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;
using Grove.Data.Connectors;
using Grove.Data.Protocols.Http;

namespace Grove.Data.Protocols.Soap;

/// <summary>
/// SOAP 1.1 described by WSDL 1.1: each operation of the first SOAP binding becomes an
/// operation (its SOAP action as <see cref="ConnectionOperation.Path"/>, its input element as
/// the request's root). A call wraps the arguments in an envelope (the <c>body</c> argument as
/// raw XML, or the arguments as child elements of the input element) and answers the body's
/// first element as JSON. A <c>Client</c> fault is permanent, a <c>Server</c> fault transient.
/// </summary>
public sealed class SoapConnectorProtocol : IConnectorProtocol
{
    public const string SoapEnvelopeNamespace = "http://schemas.xmlsoap.org/soap/envelope/";

    /// <summary>Setting: the target namespace of the operations' elements.</summary>
    public const string NamespaceSetting = "soap:namespace";

    public string Id => ConnectorProtocols.Soap;

    public string DisplayName => "SOAP (WSDL)";

    public IReadOnlyList<string> AuthSchemes { get; } =
    [
        Connectors.AuthSchemes.None,
        Connectors.AuthSchemes.Basic,
        Connectors.AuthSchemes.Bearer,
        Connectors.AuthSchemes.ApiKey,
        Connectors.AuthSchemes.OAuth2ClientCredentials,
    ];

    public IReadOnlyList<string> SchemaFormats => [ConnectionSchemaFormats.Wsdl];

    public Task<SchemaReadResult> ReadOperationsAsync(ConnectionDefinition connection, ConnectionSchemaDocument schema, CancellationToken cancellationToken = default)
    {
        try
        {
            return Task.FromResult(WsdlReader.Read(schema.Content));
        }
        catch (XmlException ex)
        {
            return Task.FromResult(new SchemaReadResult([], Warnings: [$"Not a WSDL document: {ex.Message}"]));
        }
    }

    public async Task<ProtocolResponse> SendAsync(IProtocolCallContext context, CancellationToken cancellationToken = default)
    {
        var operation = context.Operation;
        var action = context.Request?.Headers?.GetValueOrDefault("SOAPAction") ?? operation?.Path ?? string.Empty;
        var ns = context.Connection.Setting(NamespaceSetting) ?? operation?.Tags.FirstOrDefault(t => t.StartsWith("ns:", StringComparison.Ordinal))?[3..] ?? string.Empty;
        var element = operation?.Tags.FirstOrDefault(t => t.StartsWith("element:", StringComparison.Ordinal))?[8..] ?? operation?.Id ?? "Request";
        var envelope = context.Request?.Body ?? BuildEnvelope(ns, element, context.Arguments);

        using var message = new HttpRequestMessage(HttpMethod.Post, context.ResolveUri(string.Empty))
        {
            Content = new StringContent(envelope, Encoding.UTF8),
        };
        message.Content.Headers.ContentType = new MediaTypeHeaderValue("text/xml") { CharSet = "utf-8" };
        message.Headers.TryAddWithoutValidation("SOAPAction", $"\"{action}\"");
        await context.AuthorizeAsync(message, cancellationToken);

        using var response = await context.HttpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var body = await context.ReadBodyAsync(response, cancellationToken);
        var status = (int)response.StatusCode;
        var headers = HttpComposer.Headers(response);
        var (value, fault) = ReadEnvelope(body);
        if (fault is not null)
        {
            var transient = fault.Code.EndsWith("Server", StringComparison.OrdinalIgnoreCase);
            return new ProtocolResponse
            {
                StatusCode = status,
                Body = body,
                Value = value,
                Headers = headers,
                Failure = new FailureClassification(transient ? FailureKind.Transient : FailureKind.Permanent),
                Error = $"SOAP fault {fault.Code}: {fault.Message}",
            };
        }

        var failure = HttpFailures.Classify(status, response.Headers);
        return new ProtocolResponse
        {
            StatusCode = status,
            Body = body,
            Value = value,
            Headers = headers,
            Failure = failure,
            Error = failure.IsFailure ? $"HTTP {status} from {message.RequestUri?.Host}." : null,
        };
    }

    public FailureClassification Classify(Exception exception) =>
        exception is XmlException ? new FailureClassification(FailureKind.Permanent) : HttpFailures.Classify(exception);

    public static string BuildEnvelope(string ns, string element, JsonObject? arguments)
    {
        XNamespace soap = SoapEnvelopeNamespace;
        XNamespace target = ns;
        XElement content;
        if (arguments is not null && arguments.TryGetPropertyValue(HttpComposer.BodyArgument, out var raw) && raw is JsonValue rawValue && rawValue.TryGetValue<string>(out var xml))
        {
            content = XElement.Parse(xml);
        }
        else
        {
            content = new XElement(target + element, ToElements(target, arguments));
        }

        return new XDocument(new XElement(soap + "Envelope", new XAttribute(XNamespace.Xmlns + "soap", SoapEnvelopeNamespace), new XElement(soap + "Body", content))).ToString(SaveOptions.DisableFormatting);
    }

    private static IEnumerable<XElement> ToElements(XNamespace ns, JsonObject? obj)
    {
        foreach (var (name, value) in obj ?? [])
        {
            if (value is JsonArray array)
            {
                foreach (var item in array)
                {
                    yield return ToElement(ns, name, item);
                }
            }
            else
            {
                yield return ToElement(ns, name, value);
            }
        }
    }

    private static XElement ToElement(XNamespace ns, string name, JsonNode? value) => value switch
    {
        JsonObject child => new XElement(ns + name, ToElements(ns, child)),
        null => new XElement(ns + name),
        JsonValue v when v.TryGetValue<string>(out var text) => new XElement(ns + name, text),
        _ => new XElement(ns + name, value.ToJsonString()),
    };

    public sealed record SoapFault(string Code, string Message);

    /// <summary>The body's first element as JSON, and the fault when the body is one.</summary>
    public static (JsonNode? Value, SoapFault? Fault) ReadEnvelope(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return (null, null);
        }

        XDocument document;
        try
        {
            document = XDocument.Parse(body);
        }
        catch (XmlException)
        {
            return (null, null);
        }

        var bodyElement = document.Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "Body");
        var first = bodyElement?.Elements().FirstOrDefault();
        if (first is null)
        {
            return (null, null);
        }

        if (first.Name.LocalName == "Fault")
        {
            var code = first.Elements().FirstOrDefault(e => e.Name.LocalName is "faultcode" or "Code")?.Value.Trim() ?? "Server";
            var message = first.Elements().FirstOrDefault(e => e.Name.LocalName is "faultstring" or "Reason")?.Value.Trim() ?? string.Empty;
            return (ToJson(first), new SoapFault(code, message));
        }

        return (new JsonObject { [first.Name.LocalName] = ToJson(first) }, null);
    }

    /// <summary>An element as JSON: text for a leaf, an object of children (repeated names as arrays) otherwise.</summary>
    public static JsonNode? ToJson(XElement element)
    {
        if (!element.HasElements)
        {
            return element.IsEmpty ? null : JsonValue.Create(element.Value);
        }

        var obj = new JsonObject();
        foreach (var group in element.Elements().GroupBy(e => e.Name.LocalName))
        {
            var items = group.ToList();
            obj[group.Key] = items.Count == 1 ? ToJson(items[0]) : new JsonArray(items.Select(ToJson).ToArray());
        }

        return obj;
    }
}

/// <summary>Reads a WSDL 1.1 document: the service address, and the operations of its first SOAP binding.</summary>
public static class WsdlReader
{
    private static readonly XNamespace Wsdl = "http://schemas.xmlsoap.org/wsdl/";
    private static readonly XNamespace WsdlSoap = "http://schemas.xmlsoap.org/wsdl/soap/";
    private static readonly XNamespace WsdlSoap12 = "http://schemas.xmlsoap.org/wsdl/soap12/";

    public static SchemaReadResult Read(string document)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using var reader = XmlReader.Create(new StringReader(document), settings);
        var root = XDocument.Load(reader).Root ?? throw new XmlException("Empty document.");
        if (root.Name != Wsdl + "definitions")
        {
            throw new XmlException("The root is not a WSDL 1.1 'definitions' element.");
        }

        var warnings = new List<string>();
        var targetNamespace = (string?)root.Attribute("targetNamespace") ?? string.Empty;
        var title = (string?)root.Attribute("name");
        var address = root.Elements(Wsdl + "service").Elements(Wsdl + "port")
            .Select(port => (string?)port.Element(WsdlSoap + "address")?.Attribute("location") ?? (string?)port.Element(WsdlSoap12 + "address")?.Attribute("location"))
            .FirstOrDefault(location => !string.IsNullOrWhiteSpace(location));

        var binding = root.Elements(Wsdl + "binding").FirstOrDefault(b => b.Element(WsdlSoap + "binding") is not null)
            ?? root.Elements(Wsdl + "binding").FirstOrDefault();
        if (binding is null)
        {
            warnings.Add("The WSDL has no binding.");
            return new SchemaReadResult([], address, title, warnings);
        }

        if (binding.Element(WsdlSoap + "binding") is null)
        {
            warnings.Add("The first binding is not SOAP 1.1; actions are read but envelopes are sent as SOAP 1.1.");
        }

        var portTypeName = LocalName((string?)binding.Attribute("type"));
        var portType = root.Elements(Wsdl + "portType").FirstOrDefault(p => (string?)p.Attribute("name") == portTypeName);
        var messages = root.Elements(Wsdl + "message").ToDictionary(m => (string?)m.Attribute("name") ?? string.Empty, StringComparer.Ordinal);

        var operations = new List<ConnectionOperation>();
        foreach (var op in binding.Elements(Wsdl + "operation"))
        {
            var name = (string?)op.Attribute("name");
            if (name is null)
            {
                continue;
            }

            var action = (string?)op.Element(WsdlSoap + "operation")?.Attribute("soapAction") ?? (string?)op.Element(WsdlSoap12 + "operation")?.Attribute("soapAction") ?? string.Empty;
            var abstractOp = portType?.Elements(Wsdl + "operation").FirstOrDefault(o => (string?)o.Attribute("name") == name);
            var inputMessage = LocalName((string?)abstractOp?.Element(Wsdl + "input")?.Attribute("message"));
            var element = inputMessage is not null && messages.TryGetValue(inputMessage, out var message)
                ? LocalName((string?)message.Element(Wsdl + "part")?.Attribute("element")) ?? name
                : name;
            var documentation = abstractOp?.Element(Wsdl + "documentation")?.Value.Trim();

            operations.Add(new ConnectionOperation
            {
                Id = name,
                DisplayName = name,
                Description = string.IsNullOrEmpty(documentation) ? null : documentation,
                Kind = OperationKinds.Soap,
                Method = "POST",
                Path = action,
                ContentType = "text/xml",
                // A SOAP call is not safe to repeat unless the service says so; reads are named Get* by convention only.
                IsSafeToRetry = false,
                Tags = [$"element:{element}", $"ns:{targetNamespace}"],
            });
        }

        if (operations.Count == 0)
        {
            warnings.Add("The binding has no operations.");
        }

        return new SchemaReadResult(operations, address, title, warnings);
    }

    private static string? LocalName(string? qualified) =>
        qualified is null ? null : qualified.Contains(':') ? qualified[(qualified.IndexOf(':') + 1)..] : qualified;
}
