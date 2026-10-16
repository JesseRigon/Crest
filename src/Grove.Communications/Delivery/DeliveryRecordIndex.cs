using Grove.Communications.Delivery;
using YesSql.Indexes;

namespace Grove.Communications.Delivery;

public sealed class DeliveryRecordIndex : MapIndex
{
    public string DeliveryId { get; set; }

    public string Channel { get; set; }

    public string Provider { get; set; }

    public string Outcome { get; set; }

    /// <summary>The recipients, cut to <see cref="RecipientLength"/>: searched with a prefix or contains match.</summary>
    public string Recipient { get; set; }

    public DateTime CreatedUtc { get; set; }

    public const int RecipientLength = 255;
}

public sealed class DeliveryRecordIndexProvider : IndexProvider<DeliveryRecord>
{
    public DeliveryRecordIndexProvider()
    {
        CollectionName = CommunicationsConstants.DeliveryCollection;
    }

    public override void Describe(DescribeContext<DeliveryRecord> context) =>
        context.For<DeliveryRecordIndex>().Map(record => new DeliveryRecordIndex
        {
            DeliveryId = record.DeliveryId,
            Channel = record.Channel,
            Provider = Cut(record.Provider, 100),
            Outcome = record.Outcome.ToString(),
            Recipient = Cut(record.Recipient, DeliveryRecordIndex.RecipientLength),
            CreatedUtc = record.CreatedUtc,
        });

    private static string Cut(string value, int length) =>
        value is { Length: > 0 } && value.Length > length ? value[..length] : value;
}
