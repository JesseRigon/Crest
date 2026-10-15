using System.Text.Json;
using Grove.Media.Scanning.Scanners;
using Grove.Media.Scanning.Services;
using Grove.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Grove.Media.Scanning.Endpoints;

/// <summary>
/// Where offsite scanners report their verdicts (requirements 92, 94). Not a user endpoint: a
/// verdict is accepted only when signed with the shared secret of the scanner it claims to come
/// from, for a scan that scanner was asked to do. No verdict from a client is ever trusted (93).
/// </summary>
public static class VerdictCallbackEndpoint
{
    public const string Route = "api/media/scanning/verdict";

    private const int MaxBodyBytes = 64 * 1024;

    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder AddVerdictCallbackEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost(Route, HandleAsync)
            .AllowAnonymous()
            .DisableAntiforgery()
            .WithName("UploadScanVerdict")
            .ExcludeFromDescription();

        return builder;
    }

    internal static async Task<IResult> HandleAsync(
        HttpContext httpContext,
        ScannerCatalog catalog,
        ScanCoordinator coordinator,
        IClock clock,
        IOptions<UploadScanningOptions> options,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(typeof(VerdictCallbackEndpoint));

        if (httpContext.Request.ContentLength is > MaxBodyBytes)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        byte[] body;
        using (var buffer = new MemoryStream())
        {
            var chunk = new byte[8192];
            int read;
            while ((read = await httpContext.Request.Body.ReadAsync(chunk, httpContext.RequestAborted)) > 0)
            {
                if (buffer.Length + read > MaxBodyBytes)
                {
                    return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
                }

                buffer.Write(chunk, 0, read);
            }

            body = buffer.ToArray();
        }

        VerdictPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<VerdictPayload>(body, s_json);
        }
        catch (JsonException)
        {
            return Results.BadRequest();
        }

        if (payload is null || string.IsNullOrEmpty(payload.ScanId) || string.IsNullOrEmpty(payload.Scanner))
        {
            return Results.BadRequest();
        }

        var secret = await catalog.GetCallbackSecretAsync(payload.Scanner);
        var now = new DateTimeOffset(DateTime.SpecifyKind(clock.UtcNow, DateTimeKind.Utc));

        if (secret is null || !ScanSignature.Verify(
            secret,
            httpContext.Request.Headers[ScanSignature.TimestampHeader],
            httpContext.Request.Headers[ScanSignature.SignatureHeader],
            body,
            now,
            options.Value.CallbackClockSkew))
        {
            logger.LogWarning("Rejected an unsigned or wrongly signed verdict for scan '{ScanId}' from '{Scanner}'.", payload.ScanId, payload.Scanner);
            return Results.Unauthorized();
        }

        if (payload.ToVerdict() is not { } verdict)
        {
            return Results.BadRequest();
        }

        return await coordinator.ReportAsync(payload.ScanId, payload.Scanner, verdict, httpContext.RequestAborted)
            ? Results.NoContent()
            : Results.NotFound();
    }
}
