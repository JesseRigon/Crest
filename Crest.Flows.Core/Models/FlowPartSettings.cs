namespace Crest.Flows.Models;

public class FlowPartSettings
{
    public string[] ContainedContentTypes { get; set; } = [];

    public bool CollapseContainedItems { get; set; }
    public FlowAlignment? DefaultAlignment { get; set; }
}
