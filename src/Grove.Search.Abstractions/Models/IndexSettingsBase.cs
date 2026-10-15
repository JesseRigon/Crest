using System.Text.Json.Serialization;

namespace Grove.Search.Models;

public abstract class IndexSettingsBase
{
    [JsonIgnore]
    public string IndexName { get; set; }
}
