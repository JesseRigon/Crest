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
        var a = new Provider([new("order.placed", "Posted", "order", null, 10), new("party.role-created", "Role", "party", null, 5)], [new("orders.flow", "Flow A", "order", "{}", 2)]);
        var b = new Provider([new("order.placed", "Duplicate", "order", null, 1)], [new("crm.flow", "Flow B", "party", "{}", 1)]);

        var catalog = new WorkflowRegistryCatalog([a, b], [a, b], [], [], [], []);

        Assert.Equal(["party.role-created", "order.placed"], catalog.Triggers.Select(t => t.Key));
        Assert.Equal("Posted", catalog.FindTrigger("ORDER.PLACED")!.DisplayName);
        Assert.Equal(["crm.flow", "orders.flow"], catalog.Flows.Select(f => f.Key));
    }

    [Fact]
    public async Task Publisher_refuses_an_unregistered_trigger()
    {
        var sender = Substitute.For<IStimulusSender>();
        var publisher = new WorkflowTriggerPublisher(new WorkflowRegistryCatalog([], [], [], [], [], []), Queue(sender), Substitute.For<IWorkflowUserContextAccessor>(), AccessGateTests.Seal, NullLogger<WorkflowTriggerPublisher>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => publisher.PublishAsync("order.placed", "doc-1", null, TestContext.Current.CancellationToken));
        await sender.DidNotReceiveWithAnyArgs().SendAsync(default!, default(object)!, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Publisher_sends_the_stimulus_with_correlation_payload_and_user()
    {
        var provider = new Provider([new("order.placed", "Posted", "order")], []);
        var sender = Substitute.For<IStimulusSender>();
        sender.SendAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<StimulusMetadata?>(), Arg.Any<CancellationToken>())
            .Returns(new SendStimulusResult([]));
        var user = Substitute.For<IWorkflowUserContextAccessor>();
        user.Capture().Returns(new WorkflowUserContext { UserId = "u-1", UserName = "alice", Tenant = "acme" });
        user.CaptureSealed().Returns(AccessGateTests.Seal.Seal(new WorkflowUserContext { UserId = "u-1", UserName = "alice", Tenant = "acme" }));
        var publisher = new WorkflowTriggerPublisher(new WorkflowRegistryCatalog([provider], [], [], [], [], []), Queue(sender), user, AccessGateTests.Seal, NullLogger<WorkflowTriggerPublisher>.Instance);

        await publisher.PublishAsync("order.placed", "doc-1", new Dictionary<string, object> { ["Number"] = "Q-00001" }, TestContext.Current.CancellationToken);

        await sender.Received(1).SendAsync(
            "Crest.Workflows.RegistryTrigger",
            Arg.Is<object>(s => s is RegistryTriggerStimulus && ((RegistryTriggerStimulus)s).TriggerKey == "order.placed"),
            Arg.Is<StimulusMetadata?>(m => m!.CorrelationId == "doc-1"
                && (string)m.Input![WorkflowsConstants.InputKeys.TriggerKey] == "order.placed"
                && ((IDictionary<string, object>)m.Input[WorkflowsConstants.InputKeys.Payload])["Number"].Equals("Q-00001")
                && AccessGateTests.Seal.Open(m.Input[WorkflowsConstants.InputKeys.Actor], null, true)!.UserName == "alice"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_sign_in_from_an_anonymous_request_acts_as_the_user_the_event_names()
    {
        var provider = new Provider([new("user.logged-in", "Logged in", "user")], []);
        var sender = Substitute.For<IStimulusSender>();
        sender.SendAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<StimulusMetadata?>(), Arg.Any<CancellationToken>())
            .Returns(new SendStimulusResult([]));
        var user = Substitute.For<IWorkflowUserContextAccessor>();
        user.Capture().Returns(WorkflowUserContext.Anonymous("acme"));
        var publisher = new WorkflowTriggerPublisher(new WorkflowRegistryCatalog([provider], [], [], [], [], []), Queue(sender), user, AccessGateTests.Seal, NullLogger<WorkflowTriggerPublisher>.Instance);

        await publisher.PublishForUserAsync("user.logged-in", "u-5", "u-5", "bob", null, TestContext.Current.CancellationToken);

        await sender.Received(1).SendAsync(
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Is<StimulusMetadata?>(m => AccessGateTests.Seal.Open(m!.Input![WorkflowsConstants.InputKeys.Actor], null, true)!.UserId == "u-5"),
            Arg.Any<CancellationToken>());
        user.DidNotReceive().CaptureSealed();
    }

    [Fact]
    public async Task A_user_created_by_a_signed_in_caller_keeps_that_caller_as_the_actor()
    {
        var provider = new Provider([new("user.created", "Created", "user")], []);
        var sender = Substitute.For<IStimulusSender>();
        sender.SendAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<StimulusMetadata?>(), Arg.Any<CancellationToken>())
            .Returns(new SendStimulusResult([]));
        var admin = new WorkflowUserContext { UserId = "u-admin", UserName = "admin", Tenant = "acme" };
        var user = Substitute.For<IWorkflowUserContextAccessor>();
        user.Capture().Returns(admin);
        user.CaptureSealed().Returns(AccessGateTests.Seal.Seal(admin));
        var publisher = new WorkflowTriggerPublisher(new WorkflowRegistryCatalog([provider], [], [], [], [], []), Queue(sender), user, AccessGateTests.Seal, NullLogger<WorkflowTriggerPublisher>.Instance);

        await publisher.PublishForUserAsync("user.created", "u-5", "u-5", "bob", null, TestContext.Current.CancellationToken);

        await sender.Received(1).SendAsync(
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Is<StimulusMetadata?>(m => AccessGateTests.Seal.Open(m!.Input![WorkflowsConstants.InputKeys.Actor], null, true)!.UserId == "u-admin"),
            Arg.Any<CancellationToken>());
    }
}
