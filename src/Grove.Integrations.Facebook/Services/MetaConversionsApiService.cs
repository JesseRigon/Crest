using System.Text.Json.Nodes;
using Grove.Access;
using Grove.Data.Connectors;
using Grove.Integrations.Facebook.Models;
using Grove.Integrations.Facebook.Settings;
using Grove.Infrastructure;
using Grove.Infrastructure.Settings;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Grove.Integrations.Facebook.Services;

/// <summary>
/// Sends server-side events through the Meta Conversions API connection
/// (<see cref="FacebookConstants.ConversionsConnectionKey"/>): the pixel from the pixel settings,
/// the dataset's access token from the connection's tenant credential.
/// </summary>
public sealed class MetaConversionsApiService : IMetaConversionsApiService
{
    private readonly IConnectorEngine _engine;
    private readonly ISiteService _siteService;
    private readonly ICallerContextAccessor _callerAccessor;
    private readonly ICallerContextFactory _callerFactory;
    private readonly ILogger _logger;
    private readonly IStringLocalizer S;

    public MetaConversionsApiService(
        IConnectorEngine engine,
        ISiteService siteService,
        ICallerContextAccessor callerAccessor,
        ICallerContextFactory callerFactory,
        ILogger<MetaConversionsApiService> logger,
        IStringLocalizer<MetaConversionsApiService> stringLocalizer)
    {
        _engine = engine;
        _siteService = siteService;
        _callerAccessor = callerAccessor;
        _callerFactory = callerFactory;
        _logger = logger;
        S = stringLocalizer;
    }

    public async Task<Result> SendEventAsync(MetaConversionEvent conversionEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversionEvent);

        if (string.IsNullOrWhiteSpace(conversionEvent.EventName))
        {
            return Result.Failed(S["The event name is required."]);
        }

        var settings = await _siteService.GetSettingsAsync<FacebookPixelSettings>();

        if (string.IsNullOrWhiteSpace(settings.PixelId))
        {
            return Result.Failed(S["The Meta Pixel is not configured. Set the Pixel ID under Settings > Meta Pixel."]);
        }

        var arguments = new JsonObject
        {
            ["pixelId"] = settings.PixelId,
            ["data"] = new JsonArray(ToJson(conversionEvent)),
        };

        if (!string.IsNullOrWhiteSpace(settings.ConversionsApiTestEventCode))
        {
            arguments["test_event_code"] = settings.ConversionsApiTestEventCode;
        }

        // Runs as the flow's caller when it may use connectors, else as the tenant's system actor:
        // the dataset token is a tenant credential either way.
        var caller = _callerAccessor.Current is { } current && ConnectorCallers.MayCall(current)
            ? current
            : await _callerFactory.CreateSystemAsync(cancellationToken: cancellationToken);

        var result = await _engine.ExecuteAsync(new ConnectorCall
        {
            ConnectionKey = FacebookConstants.ConversionsConnectionKey,
            Caller = caller,
            OperationId = FacebookConstants.Operations.SendEvents,
            Arguments = arguments,
            Scope = CredentialScope.Tenant,
            IdempotencyKey = conversionEvent.EventId,
        }, cancellationToken);

        if (result.Succeeded)
        {
            return Result.Success();
        }

        _logger.LogError("The Meta Conversions API call ended {Outcome} ({StatusCode}): {Error}", result.Outcome, result.StatusCode, result.Error);

        return result.Outcome switch
        {
            ConnectorOutcome.NotAuthorized => Result.Failed(S["The Meta Conversions API access token is not set. Enter it on the Connections screen."]),
            ConnectorOutcome.RetryAfter => Result.Failed(S["The Meta Conversions API asked to wait until {0}.", result.RetryAt?.ToString("u") ?? string.Empty]),
            _ when result.StatusCode > 0 => Result.Failed(S["The Meta Conversions API request failed with status {0}.", result.StatusCode]),
            _ => Result.Failed(S["An error occurred connecting to the Meta Conversions API."]),
        };
    }

    internal static JsonObject ToJson(MetaConversionEvent conversionEvent)
    {
        var json = new JsonObject
        {
            ["event_name"] = conversionEvent.EventName,
            ["event_time"] = (conversionEvent.EventTime ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds(),
            ["action_source"] = ToActionSourceValue(conversionEvent.ActionSource),
        };

        if (!string.IsNullOrWhiteSpace(conversionEvent.EventSourceUrl))
        {
            json["event_source_url"] = conversionEvent.EventSourceUrl;
        }

        if (!string.IsNullOrWhiteSpace(conversionEvent.EventId))
        {
            json["event_id"] = conversionEvent.EventId;
        }

        return json;
    }

    private static string ToActionSourceValue(MetaActionSource actionSource)
        => actionSource switch
        {
            MetaActionSource.Website => "website",
            MetaActionSource.Email => "email",
            MetaActionSource.App => "app",
            MetaActionSource.PhoneCall => "phone_call",
            MetaActionSource.Chat => "chat",
            MetaActionSource.PhysicalStore => "physical_store",
            MetaActionSource.SystemGenerated => "system_generated",
            _ => "other",
        };
}
