using Grove.Workflows.Api.Client.Resources.WorkflowDefinitions.Models;

namespace Grove.Workflows.Api.Client.Resources.Tests;

public class TestActivityRequest
{
    public WorkflowDefinitionHandle WorkflowDefinitionHandle { get; set; } = null!;
    public ActivityHandle ActivityHandle { get; set; } = null!;
}