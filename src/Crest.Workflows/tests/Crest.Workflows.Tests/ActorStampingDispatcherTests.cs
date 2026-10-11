#nullable disable
using Crest.Access;
using Crest.Workflows.Contexts;
using Crest.Workflows.Runtime;
using Crest.Workflows.Runtime.Requests;
using Crest.Workflows.Runtime.Responses;
using Crest.Workflows.Security;
using NSubstitute;
using Xunit;

namespace Crest.Workflows.Tests;

// A dispatched run acts as the dispatcher's caller, never as what the request's input names
// (docs/workflows.md › The actor rule): the API's dispatch, a DispatchWorkflow activity and a
// trigger dispatch all go through this decorator.
public class ActorStampingDispatcherTests
{
    private sealed class Accessor(CallerContext current) : ICallerContextAccessor
    {
        public CallerContext Current { get; set; } = current;
    }

    private static readonly CallerContext Alice = new() { Tenant = "acme", Side = CallerSide.Admin, UserId = "u-1", UserName = "alice" };
    private static readonly CallerContext System = new() { Tenant = "acme", Side = CallerSide.System, IsSystem = true, IsSuperUser = true };

    private static (ActorStampingWorkflowDispatcher Dispatcher, IWorkflowDispatcher Inner) Build(CallerContext caller)
    {
        var inner = Substitute.For<IWorkflowDispatcher>();
        return (new ActorStampingWorkflowDispatcher(inner, new Accessor(caller), AccessGateTests.Seal), inner);
    }

    private static Dictionary<string, object> Forged() => new() { [WorkflowsConstants.InputKeys.Actor] = new WorkflowUserContext { UserId = "u-admin", UserName = "admin", Tenant = "acme" }, ["x"] = 1 };

    [Fact]
    public async Task A_new_run_carries_the_dispatchers_caller_whatever_the_input_names()
    {
        var (dispatcher, inner) = Build(Alice);
        var request = new DispatchWorkflowDefinitionRequest("v1") { Input = Forged() };

        await dispatcher.DispatchAsync(request, null, TestContext.Current.CancellationToken);

        await inner.Received().DispatchAsync(Arg.Is<DispatchWorkflowDefinitionRequest>(r =>
            AccessGateTests.Seal.Open(r.Input![WorkflowsConstants.InputKeys.Actor], null, true)!.UserId == "u-1" && (int)r.Input["x"] == 1), null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_trigger_dispatch_is_stamped_too()
    {
        var (dispatcher, inner) = Build(Alice);
        var request = new DispatchTriggerWorkflowsRequest("Activity", new object()) { Input = Forged() };

        await dispatcher.DispatchAsync(request, null, TestContext.Current.CancellationToken);

        await inner.Received().DispatchAsync(Arg.Is<DispatchTriggerWorkflowsRequest>(r =>
            AccessGateTests.Seal.Open(r.Input![WorkflowsConstants.InputKeys.Actor], null, true)!.UserId == "u-1"), null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_system_caller_is_never_stamped_as_an_actor()
    {
        var (dispatcher, inner) = Build(System);
        var request = new DispatchWorkflowDefinitionRequest("v1") { Input = Forged() };

        await dispatcher.DispatchAsync(request, null, TestContext.Current.CancellationToken);

        await inner.Received().DispatchAsync(Arg.Is<DispatchWorkflowDefinitionRequest>(r => !r.Input!.ContainsKey(WorkflowsConstants.InputKeys.Actor)), null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_resume_never_carries_an_actor()
    {
        var (dispatcher, inner) = Build(Alice);

        await dispatcher.DispatchAsync(new DispatchWorkflowInstanceRequest("i-1") { Input = Forged() }, null, TestContext.Current.CancellationToken);
        await dispatcher.DispatchAsync(new DispatchResumeWorkflowsRequest("Activity", new object(), Input: Forged()), null, TestContext.Current.CancellationToken);

        await inner.Received().DispatchAsync(Arg.Is<DispatchWorkflowInstanceRequest>(r => r.InstanceId == "i-1" && !r.Input!.ContainsKey(WorkflowsConstants.InputKeys.Actor) && r.Input.ContainsKey("x")), null, Arg.Any<CancellationToken>());
        await inner.Received().DispatchAsync(Arg.Is<DispatchResumeWorkflowsRequest>(r => !r.Input!.ContainsKey(WorkflowsConstants.InputKeys.Actor)), null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task With_no_caller_the_input_is_left_to_the_seal()
    {
        // A trigger captured in a background scope already carries a sealed actor; a forged one
        // passes through here and is refused by the gate.
        var (dispatcher, inner) = Build(null);
        var request = new DispatchWorkflowDefinitionRequest("v1") { Input = Forged() };

        await dispatcher.DispatchAsync(request, null, TestContext.Current.CancellationToken);

        await inner.Received().DispatchAsync(Arg.Is<DispatchWorkflowDefinitionRequest>(r => AccessGateTests.Seal.Open(r.Input![WorkflowsConstants.InputKeys.Actor], null, true) == null), null, Arg.Any<CancellationToken>());
    }
}
