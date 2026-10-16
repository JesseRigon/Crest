using System.Text.Json.Serialization;
using Grove.Contents.Fields.Fields;
using Grove.Contents.Management;

namespace Grove.Contents.PublishLater.Models;

/// <summary>
/// When an item is to be published. Its data is a field (<c>PublishLater.ScheduledPublishUtc</c>, a date and time
/// field); this class is the typed accessor over it. The scheduled task publishes the item when the time
/// comes, and the module's system flow clears the schedule once the item is published
/// (docs/content-engine.md › Behaviour is workflows).
/// </summary>
public class PublishLaterFieldSet : ContentFieldSet
{
    public const string ScheduledPublishUtcField = "ScheduledPublishUtc";

    [JsonIgnore]
    public DateTime? ScheduledPublishUtc
    {
        get => this.Get<DateTimeField>(ScheduledPublishUtcField)?.Value;
        set => this.Alter<DateTimeField>(ScheduledPublishUtcField, date => date.Value = value);
    }
}
