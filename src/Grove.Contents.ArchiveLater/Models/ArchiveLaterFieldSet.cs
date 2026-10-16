using System.Text.Json.Serialization;
using Grove.Contents.Fields.Fields;
using Grove.Contents.Management;

namespace Grove.Contents.ArchiveLater.Models;

/// <summary>
/// When an item is to be archiveed. Its data is a field (<c>ArchiveLater.ScheduledArchiveUtc</c>, a date and time
/// field); this class is the typed accessor over it. The scheduled task archivees the item when the time
/// comes, and the module's system flow clears the schedule once the item is archiveed
/// (docs/content-engine.md › Behaviour is workflows).
/// </summary>
public class ArchiveLaterFieldSet : ContentFieldSet
{
    public const string ScheduledArchiveUtcField = "ScheduledArchiveUtc";

    [JsonIgnore]
    public DateTime? ScheduledArchiveUtc
    {
        get => this.Get<DateTimeField>(ScheduledArchiveUtcField)?.Value;
        set => this.Alter<DateTimeField>(ScheduledArchiveUtcField, date => date.Value = value);
    }
}
