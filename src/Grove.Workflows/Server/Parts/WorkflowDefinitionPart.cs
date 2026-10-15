using Grove.Contents.Management;

namespace Grove.Workflows.Parts;

public class WorkflowDefinitionPart : ContentPart
{
    public string DefinitionId { get; set; } = null!;
    public string DefinitionVersionId { get; set; } = null!;
    public int Version { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? ProviderName { get; set; }
    public string MaterializerName { get; set; } = null!;
    //public bool IsPublished { get; set; }
    //public bool IsLatest { get; set; }
    public bool UsableAsActivity { get; set; }
    public bool IsSystem { get; set; }
    public bool IsReadonly { get; set; }
    /// <summary>Published as system: every burst runs as the tenant system actor (<see cref="WorkflowsConstants.RunsAsSystemProperty"/>; publishing it takes ManageShippedWorkflows).</summary>
    public bool RunsAsSystem { get; set; }
    /// <summary>Accepts anonymous runs from a page-URL HTTP endpoint (<see cref="WorkflowsConstants.AcceptsAnonymousRunsProperty"/>; publishing it takes ManageShippedWorkflows).</summary>
    public bool AcceptsAnonymousRuns { get; set; }
    public Version? ToolVersion { get; set; }
    public string SerializedData { get; set; } = null!;
}