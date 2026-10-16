using System.Text;
using System.Text.Json;
using Grove.Access;
using Grove.Api;
using Grove.Data.Connectors;
using Grove.Data.Connectors.Services;
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
/// authentication, checked by the connector engine's verifier (<see cref="IWebhookVerifier"/>:
/// the connection's signature scheme and header, its sealed secret, which this handler never
/// sees). Unknown or non-inbound connections are 404, a missing or wrong signature 401. The
/// engine is the tenant's, so a hook can only start this tenant's workflows. The run acts as the
/// connection's application caller (<see cref="ConnectionCaller"/>).
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
    IWebhookVerifier verifier,
    IStimulusSender stimulusSender,
    IOptions<ConnectorOptions> options,
    ICallerContextFactory callerFactory,
    IAccessRunner accessRunner,
    WorkflowActorSeal seal,
    Audit.WorkflowAuditRecorder audit,
    ILogger<WorkflowWebhooksHandlers> logger) : ApiHandlers
{
    public async Task<IResult> ReceiveAsync(string connection, string hook, CancellationToken cancellationToken)
    {
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
        var headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        var verification = await verifier.VerifyAsync(connection, new WebhookRequest(body, headers), cancellationToken);
        if (verification.Status is WebhookVerificationStatus.UnknownConnection or WebhookVerificationStatus.NotInbound)
        {
            await RejectedAsync(connection, hook, StatusCodes.Status404NotFound, "unknown or non-signing connection", body.Length);
            return NotFound();
        }

        if (!verification.Verified || verification.Connection is not { } found)
        {
            logger.LogWarning("Webhook {Connection}/{Hook} refused: {Reason}", connection, hook, verification.Error);
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
        var caller = await callerFactory.CreateAsync(new CallerRequest(ConnectionCaller.Principal(found), CallerSide.Site, null, null), cancellationToken);
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
                new Grove.AuditTrail.Models.AuditTrailSubject { RecordId = found.Key, RecordType = "Connection", DisplayText = $"{found.Key}/{hook}" },
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
            new Grove.AuditTrail.Models.AuditTrailSubject { RecordId = connection, RecordType = "Connection", DisplayText = $"{connection}/{hook}" },
            isolated: true);
}
