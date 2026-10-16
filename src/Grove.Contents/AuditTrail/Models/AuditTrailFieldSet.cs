using System.Text.Json.Serialization;
using Grove.Contents.Fields.Fields;
using Grove.Contents.Management;

namespace Grove.Contents.AuditTrail.Models;

/// <summary>
/// The comment an editor saves with a version, recorded on the version's audit trail event. Its
/// data is a text field (<c>Comment</c>); this class is the typed accessor over it.
/// </summary>
public class AuditTrailFieldSet : ContentFieldSet
{
    [JsonIgnore]
    public string Comment
    {
        get => this.Get<TextField>(nameof(Comment))?.Text;
        set => this.Alter<TextField>(nameof(Comment), fieldValue => fieldValue.Text = value);
    }}
