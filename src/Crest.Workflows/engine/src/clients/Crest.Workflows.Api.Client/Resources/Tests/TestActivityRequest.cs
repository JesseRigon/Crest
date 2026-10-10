using Crest.Workflows.Api.Client.Resources.WorkflowDefinitions.Models;

namespace Crest.Workflows.Api.Client.Resources.Tests;

public class TestActivityRequest
{
    public WorkflowDefinitionHandle WorkflowDefinitionHandle { get; set; } = null!;
    public ActivityHandle ActivityHandle { get; set; } = null!;
}