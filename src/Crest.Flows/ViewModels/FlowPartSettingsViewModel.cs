using System.Collections.Specialized;
using Crest.Flows.Models;

namespace Crest.Flows.ViewModels;

public class FlowPartSettingsViewModel
{
    public FlowPartSettings FlowPartSettings { get; set; }

    public NameValueCollection ContentTypes { get; set; }

    public string[] ContainedContentTypes { get; set; } = [];

    public bool CollapseContainedItems { get; set; }
    public FlowAlignment? DefaultAlignment { get; set; }
}
