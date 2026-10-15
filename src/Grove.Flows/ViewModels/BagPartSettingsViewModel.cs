using System.Collections.Specialized;
using Grove.Flows.Models;

namespace Grove.Flows.ViewModels;

public class BagPartSettingsViewModel
{
    public BagPartSettings BagPartSettings { get; set; }

    public NameValueCollection ContentTypes { get; set; }

    public string DisplayType { get; set; }

    public string[] ContainedContentTypes { get; set; } = [];

    public BagPartSettingType Source { get; set; }

    public string Stereotypes { get; set; }

    public bool CollapseContainedItems { get; set; }
}
