using Crest.Access;
using Crest.DisplayManagement.Components;
using Crest.Workflows.Display;
using Crest.Workflows.Security;
using NSubstitute;
using Xunit;

namespace Crest.Workflows.Tests;

// A display tree's workflow action (docs/blazor-display.md § 4a) is decided like an HTTP endpoint
// workflow: the Run permission on the definition, with its Run list as the veto beside it.
public class WorkflowDisplayActionTests
{
    private static readonly CallerContext s_viewer = CallerContext.Anonymous("Default", CallerSide.Site);

    [Fact]
    public async Task AllowsWhenTheDecisionAllowsAndTheRunListAdmits()
    {
        var filter = Filter(AccessDecision.Allowed, run: []);

        Assert.True(await filter.AllowsAsync(s_viewer, Workflow("post-invoice"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RefusesWhenTheDecisionRefuses()
    {
        var filter = Filter(AccessDecision.Denied("no"), run: []);

        Assert.False(await filter.AllowsAsync(s_viewer, Workflow("post-invoice"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RefusesWhenTheRunListLeavesTheViewerOut()
    {
        var filter = Filter(AccessDecision.Allowed, run: ["Accountant"]);

        Assert.False(await filter.AllowsAsync(s_viewer, Workflow("post-invoice"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AnswersOnlyForWorkflowActions()
    {
        var filter = Filter(AccessDecision.Allowed, run: []);

        Assert.Null(await filter.AllowsAsync(s_viewer, new DisplayAction { Kind = DisplayActionKind.Navigate }, TestContext.Current.CancellationToken));
        Assert.False(await filter.AllowsAsync(s_viewer, new DisplayAction { Kind = DisplayActionKind.Workflow }, TestContext.Current.CancellationToken));
    }

    private static DisplayAction Workflow(string definitionId) => new() { Kind = DisplayActionKind.Workflow, Workflow = definitionId };

    [Fact]
    public async Task AWorkflowWithoutAUiActionTriggerIsNotRunnableFromTheUi()
    {
        var filter = Filter(AccessDecision.Allowed, run: [], contract: null);

        Assert.False(await filter.AllowsAsync(s_viewer, Workflow("post-invoice"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void TheContractParsesItsDeclaration()
    {
        var contract = UiActionContract.Parse("invoice: string\namount: number?\nnotes\nkind: unknown");

        Assert.Equal(
            [("invoice", "string", true), ("amount", "number", false), ("notes", "any", true), ("kind", "any", true)],
            contract.Inputs.Select(input => (input.Name, input.Type, input.Required)));
    }

    [Fact]
    public void TheContractChecksNamesAndTypes()
    {
        var contract = UiActionContract.Parse("invoice: string\namount: number?");

        Assert.Empty(contract.Check(new System.Text.Json.Nodes.JsonObject { ["invoice"] = "inv-1", ["amount"] = 3 }));
        var problems = contract.Check(new System.Text.Json.Nodes.JsonObject { ["amount"] = "three", ["extra"] = true });
        Assert.Contains(problems, problem => problem.Contains("'invoice' is required", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.Contains("'extra' is not an input", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.Contains("'amount' must be a number", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheValidatorChecksATemplatesArgumentsAgainstTheContract()
    {
        var contracts = Substitute.For<IUiActionContracts>();
        contracts.FindAsync("post-invoice", Arg.Any<CancellationToken>()).Returns(UiActionContract.Parse("invoice: string"));
        var validator = new WorkflowDisplayActionValidator(contracts);

        Assert.Empty(await validator.ValidateAsync(new DisplayAction { Kind = DisplayActionKind.Workflow, Workflow = "post-invoice", Arguments = new() { ["invoice"] = DisplayBinding.FromEvent("id") } }, TestContext.Current.CancellationToken));
        Assert.NotEmpty(await validator.ValidateAsync(new DisplayAction { Kind = DisplayActionKind.Workflow, Workflow = "post-invoice" }, TestContext.Current.CancellationToken));
        Assert.NotEmpty(await validator.ValidateAsync(new DisplayAction { Kind = DisplayActionKind.Workflow, Workflow = "unexposed" }, TestContext.Current.CancellationToken));
        Assert.Empty(await validator.ValidateAsync(new DisplayAction { Kind = DisplayActionKind.Navigate, Value = DisplayBinding.Literal("x") }, TestContext.Current.CancellationToken));
    }

    private static WorkflowDisplayActionFilter Filter(AccessDecision verdict, string[] run) => Filter(verdict, run, UiActionContract.Parse(string.Empty));

    private static WorkflowDisplayActionFilter Filter(AccessDecision verdict, string[] run, UiActionContract? contract)
    {
        var decision = Substitute.For<IAccessDecision>();
        decision.DecideAsync(Arg.Any<CallerContext>(), WorkflowsConstants.Permissions.Run, Arg.Any<object?>(), Arg.Any<CancellationToken>()).Returns(verdict);
        var access = Substitute.For<IWorkflowDefinitionAccessReader>();
        access.GetAsync(Arg.Any<string>()).Returns(call => new WorkflowDefinitionAccessResource(call.Arg<string>() ?? string.Empty, [], run));
        var contracts = Substitute.For<IUiActionContracts>();
        contracts.FindAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(contract);
        return new WorkflowDisplayActionFilter(new WorkflowDisplayActionAuthorizer(decision, access), contracts);
    }
}
