using System.ComponentModel;

namespace Grove.Seo;

public class RobotsSettings
{
    [DefaultValue(true)]
    public bool AllowAllAgents { get; set; } = true;

    [DefaultValue(true)]
    public bool DisallowAdmin { get; set; } = true;

    public string AdditionalRules { get; set; }
}
