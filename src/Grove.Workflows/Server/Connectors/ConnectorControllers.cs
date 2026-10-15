using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Grove.Access;
using Grove.Security;
using Grove.Workflows.Contexts;
using Grove.Workflows.Registry;
using Grove.Workflows.Runtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Grove.Workflows.Connectors;

/// <summary>The tenant's connections. Secrets go in, never out.</summary>
[ApiController, AutoValidateAntiforgeryToken, Route(WorkflowsConstants.Routes.ConnectionsApi)]
public sealed class WorkflowConnectionsController(
    IAuthorizationService authorizationService,
    WorkflowConnectionService connections,
    WorkflowRegistryCatalog catalog,
    IAccessDecision decision,
    ICallerContextAccessor callers,
    RoleManager<IRole> roleManager) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListAsync()
    {
        if (!await CanManageAsync()) return Forbid();
        return Ok((await connections.ListAsync()).Select(connections.ToModel));
    }

    [HttpGet("{key}")]
    public async Task<IActionResult> GetAsync(string key)
    {
        if (!await CanManageAsync()) return Forbid();
        var connection = await connections.FindAsync(key);
        return connection is null ? NotFound() : Ok(connections.ToModel(connection));
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] WorkflowConnectionSaveRequest request)
    {
        if (!await CanManageAsync()) return Forbid();
        WorkflowConnectorDescriptor? connector = null;
        if (!string.IsNullOrWhiteSpace(request.ConnectorKey) && (connector = catalog.FindConnector(request.ConnectorKey)) is null)
        {
            return ValidationProblem($"No connector '{request.ConnectorKey}' is registered.");
        }

        if (await RoleRefusalAsync(request.Roles, []) is { } refusal) return Forbid(refusal);
        var (connection, errors) = await connections.SaveAsync(null, request, connector);
        return connection is null ? ValidationProblem(string.Join(" ", errors)) : Ok(connections.ToModel(connection));
    }

    [HttpPut("{key}")]
    public async Task<IActionResult> UpdateAsync(string key, [FromBody] WorkflowConnectionSaveRequest request)
    {
        if (!await CanManageAsync()) return Forbid();
        if (await connections.FindAsync(key) is not { } existing) return NotFound();
        if (await RoleRefusalAsync(request.Roles, existing.Roles) is { } refusal) return Forbid(refusal);
        var (connection, errors) = await connections.SaveAsync(key, request, null);
        return connection is null ? ValidationProblem(string.Join(" ", errors)) : Ok(connections.ToModel(connection));
    }

    [HttpDelete("{key}")]
    public async Task<IActionResult> DeleteAsync(string key)
    {
        if (!await CanManageAsync()) return Forbid();
        return await connections.DeleteAsync(key) ? NoContent() : NotFound();
    }

    /// <summary>A connection from an OpenAPI 3 document: server, security scheme and operations (one palette entry each).</summary>
    [HttpPost(WorkflowsConstants.Routes.ConnectionOpenApiImportApi)]
    public async Task<IActionResult> ImportOpenApiAsync([FromBody] WorkflowConnectionOpenApiImportRequest request)
    {
        if (!await CanManageAsync()) return Forbid();
        OpenApiImportResult import;
        try
        {
            import = OpenApiImport.Parse(request.Document ?? string.Empty);
        }
        catch (JsonException ex)
        {
            return ValidationProblem($"Not an OpenAPI 3 document: {ex.Message}");
        }

        var settings = new Dictionary<string, string>(import.Settings, StringComparer.OrdinalIgnoreCase)
        {
            [WorkflowConnectionSettingKeys.Operations] = JsonSerializer.Serialize(import.Operations, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
        };
        var (connection, errors) = await connections.SaveAsync(null, new WorkflowConnectionSaveRequest(
            request.Key, request.DisplayName ?? import.Title, null, import.BaseUrl, import.AuthKind, settings, request.Secret, null, null, null), null);
        return connection is null
            ? ValidationProblem(string.Join(" ", errors.Concat(import.Warnings)))
            : Ok(new { Connection = connections.ToModel(connection), import.Warnings });
    }

    private async Task<bool> CanManageAsync() => await authorizationService.AuthorizeAsync(User, Permissions.ManageConnections);

    /// <summary>
    /// Assigning a role to a connection's machine identity is a rights write: each role added
    /// takes the right to assign that role (<c>AssignRoleToUsers</c> against the role), as it
    /// does for a user, so managing connections never grants more than the manager may give.
    /// </summary>
    private async Task<string?> RoleRefusalAsync(IReadOnlyList<string>? requested, IReadOnlyList<string> current)
    {
        if (requested is null)
        {
            return null;
        }

        foreach (var roleName in requested.Where(role => !string.IsNullOrWhiteSpace(role)).Select(role => role.Trim()).Except(current, StringComparer.OrdinalIgnoreCase))
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role is null)
            {
                return $"No role '{roleName}'.";
            }

            if (!await decision.AllowsAsync(callers.Current, AssignRoleToUsersPermission, role, HttpContext.RequestAborted))
            {
                return $"Assigning role '{roleName}' to a connection takes the right to assign it to users.";
            }
        }

        return null;
    }

    private const string AssignRoleToUsersPermission = "AssignRoleToUsers";

    private ObjectResult Forbid(string detail) => StatusCode(StatusCodes.Status403Forbidden, new { title = "Forbidden.", detail });

    private BadRequestObjectResult ValidationProblem(string detail) => BadRequest(new { title = "Invalid connection.", detail });
}

/// <summary>
/// Inbound webhooks. Anonymous by design (the caller is an external service), so the
/// signature is the authentication: HMAC-SHA256 of the raw body with the connection's
/// secret, hex, in the connection's signature header (optionally prefixed <c>sha256=</c>).
/// Unknown or non-hmac connections are 404, a missing or wrong signature 401. The engine is
/// the tenant's, so a hook can only start this tenant's workflows. The run acts as the
/// connection's application caller (<see cref="WorkflowConnectionCaller"/>).
/// </summary>
[ApiController, AllowAnonymous, IgnoreAntiforgeryToken, Route(WorkflowsConstants.Routes.WebhooksApi)]
public sealed class WorkflowWebhooksController(
    WorkflowConnectionService connections,
    IStimulusSender stimulusSender,
    IOptions<WorkflowConnectorOptions> options,
    ICallerContextFactory callerFactory,
    IAccessRunner accessRunner,
    WorkflowActorSeal seal,
    Audit.WorkflowAuditRecorder audit,
    ILogger<WorkflowWebhooksController> logger) : ControllerBase
{
    [HttpPost("{connection}/{hook}")]
    public async Task<IActionResult> ReceiveAsync(string connection, string hook, CancellationToken cancellationToken)
    {
        var found = await connections.FindAsync(connection);
        if (found is null || found.AuthKind != WorkflowConnectorAuthKinds.Hmac || connections.RevealSecret(found) is not { } secret)
        {
            await RejectedAsync(connection, hook, StatusCodes.Status404NotFound, "unknown or non-signing connection", Request.ContentLength);
            return NotFound();
        }

        var max = options.Value.MaxWebhookBytes;
        if (Request.ContentLength > max)
        {
            await RejectedAsync(connection, hook, StatusCodes.Status413PayloadTooLarge, "payload too large", Request.ContentLength);
            return StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        using var buffer = new MemoryStream();
        await Request.Body.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length > max)
        {
            await RejectedAsync(connection, hook, StatusCodes.Status413PayloadTooLarge, "payload too large", buffer.Length);
            return StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        var body = buffer.ToArray();
        var headerName = found.Setting(WorkflowConnectionSettingKeys.SignatureHeader) ?? WorkflowConnectionSettingKeys.DefaultSignatureHeader;
        if (!IsValidSignature(Request.Headers[headerName].ToString(), body, secret))
        {
            logger.LogWarning("Webhook {Connection}/{Hook} refused: missing or invalid signature.", connection, hook);
            await RejectedAsync(connection, hook, StatusCodes.Status401Unauthorized, "missing or invalid signature", body.Length);
            return Unauthorized();
        }

        var text = Encoding.UTF8.GetString(body);
        object? parsed = text;
        if (Request.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true && text.Length > 0)
        {
            try
            {
                using var json = JsonDocument.Parse(text);
                parsed = Services.JsonPlain.ToPlain(json.RootElement.Clone());
            }
            catch (JsonException)
            {
                await RejectedAsync(connection, hook, StatusCodes.Status400BadRequest, "the body is not valid JSON", body.Length);
                return BadRequest(new { title = "The body is not valid JSON." });
            }
        }

        var payload = new Dictionary<string, object>
        {
            ["Body"] = parsed ?? string.Empty,
            ["Query"] = Request.Query.ToDictionary(q => q.Key, q => (object)q.Value.ToString()),
            ["ContentType"] = Request.ContentType ?? string.Empty,
        };
        // The signature authenticated the connection: the run acts as its machine identity, with
        // the roles assigned on the connection, never as the anonymous request (docs/access.md,
        // Anonymous actors).
        var caller = await callerFactory.CreateAsync(new CallerRequest(WorkflowConnectionCaller.Principal(found), CallerSide.Site, null, null), cancellationToken);
        var input = new Dictionary<string, object>
        {
            [WorkflowsConstants.InputKeys.Payload] = payload,
            [WorkflowsConstants.InputKeys.Actor] = seal.Seal(WorkflowUserContext.From(caller)),
        };

        var result = await accessRunner.RunAsAsync(caller, async () =>
        {
            var sent = await stimulusSender.SendAsync<WebhookReceived>(WebhookReceived.Stimulus(found.Key, hook), new() { Input = input }, cancellationToken);
            // Recorded as the connection's machine identity (docs/machine-actors.md).
            await audit.RecordAsync(
                Audit.WorkflowAuditTrailEventConfiguration.WebhookReceived,
                found.Key,
                new Audit.WorkflowAuditTrailEvent { Connection = found.Key, Hook = hook, StatusCode = StatusCodes.Status202Accepted, Bytes = body.Length, Started = sent.WorkflowInstanceResponses.Count },
                new Grove.AuditTrail.Models.AuditTrailSubject { RecordId = found.Key, RecordType = "WorkflowConnection", DisplayText = $"{found.Key}/{hook}" },
                isolated: false);
            return sent;
        });
        return Accepted(new { started = result.WorkflowInstanceResponses.Count });
    }

    /// <summary>
    /// A refused delivery (docs/workflows.md › Run history and insight through the audit system:
    /// rejected posts are recorded, not only logged). The caller is anonymous; the event says
    /// which connection and hook were addressed, why it was refused and how large it was -
    /// never the body.
    /// </summary>
    private Task RejectedAsync(string connection, string hook, int statusCode, string reason, long? bytes) =>
        audit.RecordAsync(
            Audit.WorkflowAuditTrailEventConfiguration.WebhookRejected,
            connection,
            new Audit.WorkflowAuditTrailEvent { Connection = connection, Hook = hook, StatusCode = statusCode, Reason = reason, Bytes = bytes },
            new Grove.AuditTrail.Models.AuditTrailSubject { RecordId = connection, RecordType = "WorkflowConnection", DisplayText = $"{connection}/{hook}" },
            isolated: true);

    public static bool IsValidSignature(string header, byte[] body, string secret)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return false;
        }

        var value = header.Trim();
        if (value.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            value = value[7..];
        }

        byte[] given;
        try
        {
            given = Convert.FromHexString(value);
        }
        catch (FormatException)
        {
            return false;
        }

        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body);
        return CryptographicOperations.FixedTimeEquals(given, expected);
    }
}
