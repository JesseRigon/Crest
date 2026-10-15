using System.Text.Json;
using Grove.Workflows.Samples;
using Xunit;

namespace Grove.Workflows.Tests;

// The workflow fixtures the browser checks run against: a system flow and a shipped draft,
// both started by the sample content-published trigger.
public sealed class WorkflowSamplesTests
{
    [Fact]
    public void TheSamplesShipASystemFlowAndADraftTemplateOnTheSampleTrigger()
    {
        var provider = new WorkflowSamplesProvider();

        Assert.Equal([WorkflowSamples.ContentPublishedTrigger], provider.Triggers.Select(trigger => trigger.Key));
        var system = Assert.Single(provider.Flows, flow => flow.Key == WorkflowSamples.SystemFlowKey);
        var shipped = Assert.Single(provider.Flows, flow => flow.Key == WorkflowSamples.ShippedFlowKey);
        Assert.Equal((WorkflowOwnership.System, true), (system.Ownership, system.Publish));
        Assert.Equal((WorkflowOwnership.Shipped, false), (shipped.Ownership, shipped.Publish));

        foreach (var flow in provider.Flows)
        {
            using var definition = JsonDocument.Parse(flow.DefinitionJson);
            var trigger = definition.RootElement.GetProperty("root").GetProperty("activities")[0];
            Assert.Equal("Grove.Workflows.RegistryTrigger", trigger.GetProperty("type").GetString());
            Assert.Equal(WorkflowSamples.ContentPublishedTrigger, trigger.GetProperty("triggerKey").GetProperty("expression").GetProperty("value").GetString());
        }
    }
}
