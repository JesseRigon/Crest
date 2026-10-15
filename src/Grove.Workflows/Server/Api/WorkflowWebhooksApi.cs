using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Grove.Access;
using Grove.Api;
using Grove.Security;
using Grove.Workflows.Connectors;
using Grove.Workflows.Contexts;
using Grove.Workflows.Registry;
using Grove.Workflows.Runtime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Grove.Workflows.Api;

/// <summary>
/// <c>api/grove/workflows/webhooks/{connection}/{hook}</c>: inbound webhooks. Anonymous by design
/// (the caller is an external service) and outside the antiforgery check, so the signature is the
/// authentication: HMAC-SHA256 of the raw body with the connection's secret, hex, in the
/// connection's signature header (optionally prefixed <c>sha256=</c>). Unknown or non-hmac
/// connections are 404, a missing or wrong signature 401. The engine is the tenant's, so a hook
/// can only start this tenant's workflows. The run acts as the connection's application caller
/// (<see cref="WorkflowConnectionCaller"/>).
/// </summary>
public sealed class WorkflowWebhooksApi : ApiContributor
{
    public override string Name => "WorkflowWebhooks";

    public override string FeatureId => WorkflowsConstants.FeatureId;

    public override void Map(IEndpointRouteBuilder group) =>
        group.MapPost("workflows/webhooks/{connection}/{hook}", (HttpContext http, [FromRoute] string connection, [FromRoute] string hook, CancellationToken cancellationToken) =>
            ApiHandlers.For<WorkflowWebhooksHandlers>(http).ReceiveAsync(connection, hook, cancellationToken))
            .AllowAnonymous()
            .SkipApiAntiforgery();
}

public sealed class WorkflowWebhooksHandlers(
    WorkflowConnectionService connections,
    IStimulusSender stimulusSender,
    IOptions<WorkflowConnectorOptions> options,
    ICallerContextFactory callerFactory,
    IAccessRunner accessRunner,
    WorkflowActorSeal seal,
    Audit.WorkflowAuditRecorder audit,
    ILogger<WorkflowWebhooksHandlers> logger) : ApiHandlers
{
    public async Task<IResult> ReceiveAsync(string connection, string hook, CancellationToken cancellationToken)
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
                parsed = Grove.Workflows.Services.JsonPlain.ToPlain(json.RootElement.Clone());
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
        return StatusCode(StatusCodes.Status202Accepted, new { started = result.WorkflowInstanceResponses.Count });
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
