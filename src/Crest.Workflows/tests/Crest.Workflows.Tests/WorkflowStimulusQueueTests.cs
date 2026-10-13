using Crest.Workflows.Registry;
using Crest.Workflows.Runtime;
using Crest.Workflows.Runtime.Results;
using Crest.Workflows.Units;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Crest.Workflows.Tests;

// The event queue (docs/workflows.md › Queues, the event side): a stimulus is keyed by the
// unit that raised it and its slot there, and what is written comes back with the types the
// raiser passed, so a delivery after commit hands the flows the same input.
public class WorkflowStimulusQueueTests
{
    [Fact]
    public async Task The_idempotency_key_is_the_unit_and_the_slot()
    {
        var sender = Substitute.For<IStimulusSender>();
        sender.SendAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<StimulusMetadata?>(), Arg.Any<CancellationToken>()).Returns(new SendStimulusResult([]));
        var unit = new WorkflowUnitOfWork();
        var queue = new WorkflowStimulusQueue(sender, new WorkflowAfterCommit(unit, NullLogger<WorkflowAfterCommit>.Instance), NullLogger<WorkflowStimulusQueue>.Instance, unit);

        var first = new StimulusMetadata { CorrelationId = "order-1" };
        var second = new StimulusMetadata { CorrelationId = "order-1" };
        await queue.EnqueueAsync<RegistryTrigger>(new RegistryTriggerStimulus("order.placed"), first);
        await queue.EnqueueAsync<RegistryTrigger>(new RegistryTriggerStimulus("order.placed"), second);

        Assert.Equal($"{unit.Id}:1", first.Input![WorkflowsConstants.InputKeys.StimulusId]);
        Assert.Equal($"{unit.Id}:2", second.Input![WorkflowsConstants.InputKeys.StimulusId]);
    }

    [Fact]
    public void A_written_value_reads_back_as_its_type()
    {
        var stimulus = WorkflowStimulusValue.From(new RegistryTriggerStimulus("order.placed"));
        var payload = WorkflowStimulusValue.From(new Dictionary<string, object> { ["OrderId"] = "o-1", ["Lines"] = 3, ["Paid"] = true });
        var text = WorkflowStimulusValue.From("sealed-actor");

        Assert.Equal(new RegistryTriggerStimulus("order.placed"), stimulus.ToValue());
        var plain = Assert.IsType<Dictionary<string, object>>(payload.ToValue());
        Assert.Equal("o-1", plain["OrderId"]);
        Assert.Equal(3L, plain["Lines"]);
        Assert.Equal(true, plain["Paid"]);
        Assert.Equal("sealed-actor", text.ToValue());
        Assert.Null(WorkflowStimulusValue.From(null).ToValue());
    }
}
