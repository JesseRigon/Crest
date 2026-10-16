using Grove.Security.Permissions;

namespace Grove.Communications.Delivery;

/// <summary>One send through a channel, as the delivery log keeps it.</summary>
public sealed class DeliveryRecord
{
    public string DeliveryId { get; set; } = string.Empty;

    /// <summary>The channel (<see cref="DeliveryChannels"/>).</summary>
    public string Channel { get; set; } = string.Empty;

    /// <summary>The provider's technical name, or its display name when the channel knows no other.</summary>
    public string? Provider { get; set; }

    public string? ConnectionKey { get; set; }

    /// <summary>The recipients as the message named them.</summary>
    public string? Recipient { get; set; }

    /// <summary>The e-mail subject; null for SMS.</summary>
    public string? Subject { get; set; }

    public DeliveryOutcome Outcome { get; set; }

    public int AttemptCount { get; set; }

    public List<DeliveryAttempt> Attempts { get; set; } = [];

    public string? Error { get; set; }

    public DateTimeOffset? RetryAt { get; set; }

    public DateTime CreatedUtc { get; set; }
}

/// <summary>The channel ids the delivery log knows.</summary>
public static class DeliveryChannels
{
    public const string Email = "email";

    public const string Sms = "sms";
}

/// <summary>A filter and page of the delivery log; newest first.</summary>
public sealed record DeliveryLogQuery
{
    public string? Channel { get; init; }

    public DeliveryOutcome? Outcome { get; init; }

    /// <summary>A part of the recipient.</summary>
    public string? Recipient { get; init; }

    public DateTime? FromUtc { get; init; }

    public DateTime? ToUtc { get; init; }

    public int Skip { get; init; }

    public int Take { get; init; } = 50;
}

public sealed record DeliveryLogPage(int Total, IReadOnlyList<DeliveryRecord> Records);

/// <summary>
/// The delivery log (Grove.Communications' delivery log feature). The channel services write
/// each send; it is absent when the feature is off, so a channel resolves it optionally.
/// </summary>
public interface IDeliveryLog
{
    /// <summary>Records a send in a unit of its own: it is kept even when the caller's unit fails.</summary>
    Task RecordAsync(DeliveryRecord record, CancellationToken cancellationToken = default);

    Task<DeliveryLogPage> ListAsync(DeliveryLogQuery query, CancellationToken cancellationToken = default);
}

/// <summary>A provider that sends or receives through a connection: the communications screens show which.</summary>
public interface IConnectedProvider
{
    /// <summary>The connection the provider uses now; null when it has none (or needs none).</summary>
    string? ConnectionKey { get; }
}

public static class CommunicationsPermissions
{
    public static readonly Permission ViewDeliveryLog = new("ViewDeliveryLog", "View the communications delivery log");
}

public static class CommunicationsConstants
{
    /// <summary>The feature of the communications screens (the Communications menu section) and the delivery log.</summary>
    public const string ScreensFeatureId = "Grove.Communications.Screens";

    public const string DeliveryCollection = "Delivery";

    /// <summary>The page region each channel contributes its card to on the Communications page.</summary>
    public const string ChannelsRegion = "Communications.Channels";

    /// <summary>The admin menu section of the communications screens.</summary>
    public const string MenuSection = "communications";
}

/// <summary>Writes a channel's send to the delivery log (when the log is on).</summary>
public static class DeliveryLogExtensions
{
    public static Task RecordAsync(
        this IDeliveryLog? log,
        string channel,
        string? provider,
        string? recipient,
        string? subject,
        Grove.Infrastructure.Result result,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        if (log is null || result is null)
        {
            return Task.CompletedTask;
        }

        var delivery = result as DeliveryResult;
        var error = delivery?.Error ?? (result.Succeeded ? null : string.Join("; ", result.Errors.Select(error => error.Message?.Value)));

        return log.RecordAsync(new DeliveryRecord
        {
            DeliveryId = Grove.IdGenerator.GenerateId(),
            Channel = channel,
            Provider = provider,
            ConnectionKey = delivery?.ConnectionKey,
            Recipient = recipient,
            Subject = subject,
            Outcome = delivery?.Outcome ?? (result.Succeeded ? DeliveryOutcome.Sent : DeliveryOutcome.Failed),
            AttemptCount = delivery?.Attempts.Count ?? (result.Succeeded ? 1 : 0),
            Attempts = [.. delivery?.Attempts ?? []],
            Error = error,
            RetryAt = delivery?.RetryAt,
            CreatedUtc = nowUtc,
        }, cancellationToken);
    }
}
