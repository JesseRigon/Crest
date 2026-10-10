using System.Text.Json.Serialization;

namespace Crest.Search.Models;

public abstract class IndexSettingsBase
{
    [JsonIgnore]
    public string IndexName { get; set; }
}
