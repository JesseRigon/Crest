using Grove.Data.Connectors;
using Grove.Infrastructure;

namespace Grove.Communications.Delivery;

/// <summary>How a send ended, as the delivery log records it.</summary>
public enum DeliveryOutcome
{
    /// <summary>The provider's service accepted the message (or a local provider wrote it).</summary>
    Sent,

    /// <summary>The send failed: the service refused it, every retry failed, or the message was invalid.</summary>
    Failed,

    /// <summary>The service asked to try again later; nothing was sent.</summary>
    Deferred,

    /// <summary>Refused before anything was sent (an address the guard refuses, a size cap).</summary>
    Refused,

    /// <summary>The provider's connection has no usable credential.</summary>
    NotAuthorized,
}

/// <summary>One attempt the connector engine made for a send.</summary>
public sealed record DeliveryAttempt(int Number, DateTime StartedUtc, TimeSpan Duration, int StatusCode, string? Failure, string? Error);

/// <summary>
/// A provider's send result with what the connector engine reported: the outcome, the
/// connection and the attempts. A provider over a connection returns it so the channel service
/// can write the delivery log; a plain <see cref="Result"/> is logged with its success alone.
/// </summary>
public sealed class DeliveryResult : Result
{
    private DeliveryResult(bool succeeded, IEnumerable<ResultError> errors)
        : base(errors)
    {
        Succeeded = succeeded;
    }

    public DeliveryOutcome Outcome { get; private init; }

    public string? ConnectionKey { get; private init; }

    public IReadOnlyList<DeliveryAttempt> Attempts { get; private init; } = [];

    /// <summary>The engine's or the service's error (never a secret).</summary>
    public string? Error { get; private init; }

    /// <summary>Set with <see cref="DeliveryOutcome.Deferred"/>.</summary>
    public DateTimeOffset? RetryAt { get; private init; }

    /// <summary>
    /// The result of a send the engine ran. <paramref name="succeeded"/> may be stricter than the
    /// call's success (a service that answered with a failed status).
    /// </summary>
    public static DeliveryResult From(ConnectorResult result, string connectionKey, bool succeeded, params IEnumerable<ResultError> errors)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new DeliveryResult(succeeded, succeeded ? [] : errors)
        {
            Outcome = succeeded ? DeliveryOutcome.Sent : result.Outcome switch
            {
                ConnectorOutcome.RetryAfter => DeliveryOutcome.Deferred,
                ConnectorOutcome.Refused => DeliveryOutcome.Refused,
                ConnectorOutcome.NotAuthorized => DeliveryOutcome.NotAuthorized,
                _ => DeliveryOutcome.Failed,
            },
            ConnectionKey = connectionKey,
            Attempts = [.. result.Attempts.Select(attempt => new DeliveryAttempt(
                attempt.Number,
                attempt.StartedUtc,
                attempt.Duration,
                attempt.StatusCode,
                attempt.Failure == FailureKind.None ? null : attempt.Failure.ToString(),
                attempt.Error))],
            Error = succeeded ? null : result.Error ?? errors.FirstOrDefault()?.Message?.Value,
            RetryAt = result.RetryAt,
        };
    }
}
