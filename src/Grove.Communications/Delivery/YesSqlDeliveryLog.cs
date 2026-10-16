using Grove.Communications.Delivery;
using Microsoft.Extensions.Logging;
using YesSql;
using YesSql.Services;

namespace Grove.Communications.Delivery;

/// <summary>The delivery log in the tenant's store, one document per send in its own session.</summary>
public sealed class YesSqlDeliveryLog(IStore store, ILogger<YesSqlDeliveryLog> logger) : IDeliveryLog
{
    public const int MaxTake = 200;

    public async Task RecordAsync(DeliveryRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        try
        {
            await using var session = store.CreateSession();
            await session.SaveAsync(record, collection: CommunicationsConstants.DeliveryCollection, cancellationToken: cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The send happened (or failed) regardless; a lost log entry must not fail it.
            logger.LogError(exception, "The {Channel} delivery to {Recipient} could not be written to the delivery log.", record.Channel, record.Recipient);
        }
    }

    public async Task<DeliveryLogPage> ListAsync(DeliveryLogQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        await using var session = store.CreateSession();
        var results = session.Query<DeliveryRecord, DeliveryRecordIndex>(collection: CommunicationsConstants.DeliveryCollection);

        if (!string.IsNullOrEmpty(query.Channel))
        {
            var channel = query.Channel;
            results = results.Where(index => index.Channel == channel);
        }

        if (query.Outcome is { } outcome)
        {
            var value = outcome.ToString();
            results = results.Where(index => index.Outcome == value);
        }

        if (!string.IsNullOrWhiteSpace(query.Recipient))
        {
            var recipient = query.Recipient.Trim();
            results = results.Where(index => index.Recipient.Contains(recipient));
        }

        if (query.FromUtc is { } from)
        {
            results = results.Where(index => index.CreatedUtc >= from);
        }

        if (query.ToUtc is { } to)
        {
            results = results.Where(index => index.CreatedUtc <= to);
        }

        var total = await results.CountAsync(cancellationToken);
        var page = await results
            .OrderByDescending(index => index.CreatedUtc)
            .Skip(Math.Max(query.Skip, 0))
            .Take(Math.Clamp(query.Take, 1, MaxTake))
            .ListAsync(cancellationToken);

        return new DeliveryLogPage(total, [.. page]);
    }
}
