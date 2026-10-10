using Crest.Workflows.Contexts;
using Crest.Workflows.Registry;
using Crest.Workflows.Runtime;
using Crest.Workflows.Runtime.Results;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Crest.Workflows.Tests;

// The fifth registry's contract: providers merge by key (first wins, ordered), and the one
// call a module makes - publish a trigger - refuses unregistered keys, lower-cases the key,
// correlates to the object and carries the payload and the acting user as workflow input.
public class WorkflowRegistryTests
{
    // Outside a shell scope the queue sends immediately, which is what these tests observe.
    private static Units.WorkflowStimulusQueue Queue(IStimulusSender sender) =>
        new(sender, new Units.WorkflowAfterCommit(new Units.WorkflowUnitOfWork(), NullLogger<Units.WorkflowAfterCommit>.Instance), NullLogger<Units.WorkflowStimulusQueue>.Instance);

    private sealed class Provider(WorkflowTriggerDescriptor[] triggers, WorkflowFlowDescriptor[] flows) : IWorkflowTriggerProvider, IWorkflowFlowProvider
    {
        public IEnumerable<WorkflowTriggerDescriptor> Triggers => triggers;
        public IEnumerable<WorkflowFlowDescriptor> Flows => flows;
    }

    [Fact]
    public void Catalog_merges_providers_first_wins_and_orders_by_position_then_key()
    {
        var a = new Provider([new("transaction.posted", "Posted", "transaction", null, 10), new("party.role-created", "Role", "party", null, 5)], [new("acc.flow", "Flow A", "transaction", "{}", 2)]);
        var b = new Provider([new("Transaction.Posted", "Duplicate", "transaction", null, 1)], [new("crm.flow", "Flow B", "party", "{}", 1)]);

        var catalog = new WorkflowRegistryCatalog([a, b], [a, b], [], [], [], []);

        Assert.Equal(["party.role-created", "transaction.posted"], catalog.Triggers.Select(t => t.Key));
        Assert.Equal("Posted", catalog.FindTrigger("TRANSACTION.POSTED")!.DisplayName);
        Assert.Equal(["crm.flow", "acc.flow"], catalog.Flows.Select(f => f.Key));
    }

    [Fact]
    public async Task Publisher_refuses_an_unregistered_trigger()
    {
        var sender = Substitute.For<IStimulusSender>();
        var publisher = new WorkflowTriggerPublisher(new WorkflowRegistryCatalog([], [], [], [], [], []), Queue(sender), Substitute.For<IWorkflowUserContextAccessor>(), NullLogger<WorkflowTriggerPublisher>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => publisher.PublishAsync("transaction.posted", "doc-1", null, TestContext.Current.CancellationToken));
        await sender.DidNotReceiveWithAnyArgs().SendAsync(default!, default(object)!, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Publisher_sends_the_stimulus_with_correlation_payload_and_user()
    {
        var provider = new Provider([new("transaction.posted", "Posted", "transaction")], []);
        var sender = Substitute.For<IStimulusSender>();
        sender.SendAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<StimulusMetadata?>(), Arg.Any<CancellationToken>())
            .Returns(new SendStimulusResult([]));
        var user = Substitute.For<IWorkflowUserContextAccessor>();
        user.Capture().Returns(new WorkflowUserContext { IsAuthenticated = true, UserName = "alice", Tenant = "acme" });
        var publisher = new WorkflowTriggerPublisher(new WorkflowRegistryCatalog([provider], [], [], [], [], []), Queue(sender), user, NullLogger<WorkflowTriggerPublisher>.Instance);

        await publisher.PublishAsync("Transaction.Posted", "doc-1", new Dictionary<string, object> { ["Number"] = "Q-00001" }, TestContext.Current.CancellationToken);

        await sender.Received(1).SendAsync(
            "Crest.Workflows.CrestTrigger",
            Arg.Is<object>(s => s is CrestTriggerStimulus && ((CrestTriggerStimulus)s).TriggerKey == "transaction.posted"),
            Arg.Is<StimulusMetadata?>(m => m!.CorrelationId == "doc-1"
                && (string)m.Input![WorkflowsConstants.InputKeys.TriggerKey] == "transaction.posted"
                && ((IDictionary<string, object>)m.Input[WorkflowsConstants.InputKeys.Payload])["Number"].Equals("Q-00001")
                && ((WorkflowUserContext)m.Input[WorkflowsConstants.InputKeys.Actor]).UserName == "alice"),
            Arg.Any<CancellationToken>());
    }
}
