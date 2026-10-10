using Crest.Access;
using Crest.Indexing;
using Crest.Services;
using Xunit;

namespace Crest.Server.Tests;

// Assignment matching: an item may be assigned to several targets at once, and a
// query asks for any of them or all of them.
public class CrestAssignmentMatchingTests
{
    private static CrestAssignmentIndex Row(string itemId, string kind, string targetId) =>
        new() { ContentItemId = itemId, Kind = kind, TargetId = targetId, Latest = true };

    [Fact]
    public void A_requirement_without_a_kind_matches_any_role()
    {
        var row = Row("item-1", "SalesRep", "user-1");

        Assert.True(CrestAssignmentService.Satisfies(row, new AssignmentRequirement("user-1")));
    }

    [Fact]
    public void A_requirement_with_a_kind_must_match_that_role()
    {
        var row = Row("item-1", "SalesRep", "user-1");

        Assert.True(CrestAssignmentService.Satisfies(row, new AssignmentRequirement("user-1", "SalesRep")));

        // Same target, different role - a technician assignment must not satisfy a
        // sales-rep requirement.
        Assert.False(CrestAssignmentService.Satisfies(row, new AssignmentRequirement("user-1", "Technician")));
    }

    [Fact]
    public void A_different_target_never_matches()
    {
        var row = Row("item-1", "SalesRep", "user-1");

        Assert.False(CrestAssignmentService.Satisfies(row, new AssignmentRequirement("user-2")));
    }

    [Fact]
    public void Target_and_kind_comparisons_ignore_case()
    {
        var row = Row("item-1", "SalesRep", "user-1");

        Assert.True(CrestAssignmentService.Satisfies(row, new AssignmentRequirement("USER-1", "salesrep")));
    }
}

// Scope combination. The distinction that carries the security weight is
// null AssignedIds ("no assignment constraint") vs empty ("constrained, nothing
// matched") - conflating them inverts the restriction.
public class AssignmentScopeTests
{
    [Fact]
    public void Unconstrained_scope_admits_every_item()
    {
        // A rule that says nothing about assignment admits the item: "no requirement" is
        // not "nothing matched".
        var rule = ScopeRule.All;

        Assert.True(rule.Admits(column => column == "ContentItemId" ? "item-1" : "Customer"));
    }

    [Fact]
    public void Constrained_scope_with_nothing_matched_admits_nothing()
    {
        // The AssignmentScopeProvider drops the type's branch when nothing is assigned, so
        // the remaining rule admits no row of that type: constrained and empty is a denial.
        var rule = ScopeRule.Where(ScopeFilter.Of(ScopeCondition.NotIn("ContentType", ["Customer"])));

        Assert.False(rule.Admits(column => column == "ContentType" ? "Customer" : "item-1"));
        Assert.True(rule.Admits(column => column == "ContentType" ? "Vendor" : "item-1"));
    }

    [Fact]
    public void Assignment_scope_admits_only_assigned_ids_of_the_scoped_type()
    {
        var rule = ScopeRule.Where(ScopeFilter.AnyOf(
            ScopeFilter.Of(ScopeCondition.NotIn("ContentType", ["Customer"])),
            ScopeFilter.AllOf(
                ScopeFilter.Of(ScopeCondition.Equal("ContentType", "Customer")),
                ScopeFilter.Of(ScopeCondition.In("ContentItemId", ["item-1"])))));

        Assert.True(rule.Admits(column => column == "ContentType" ? "Customer" : "item-1"));
        Assert.False(rule.Admits(column => column == "ContentType" ? "Customer" : "item-2"));
    }

    [Fact]
    public void Assignment_never_widens_what_the_permission_check_granted()
    {
        // Assigned, but the user cannot view this type at all: the content rule says None and
        // the assignment rule is conjoined, so assignment is never an alternative route in.
        var rule = ScopeRule.None.And(ScopeRule.Where(ScopeFilter.Of(ScopeCondition.In("ContentItemId", ["item-1"]))));

        Assert.Equal(ScopeKind.None, rule.Kind);
        Assert.False(rule.Admits(column => column == "ContentItemId" ? "item-1" : "Customer"));
    }

    [Fact]
    public void Assignment_still_requires_ownership_when_only_own_is_granted()
    {
        var ownership = ScopeRule.Where(ScopeFilter.AllOf(
            ScopeFilter.Of(ScopeCondition.In("ContentType", ["Customer"])),
            ScopeFilter.Of(ScopeCondition.Equal("Owner", "user-1"))));
        var assignment = ScopeRule.Where(ScopeFilter.Of(ScopeCondition.In("ContentItemId", ["item-1"])));
        var rule = ownership.And(assignment);

        object? Row(string owner, string column) => column switch
        {
            "ContentType" => "Customer",
            "Owner" => owner,
            "ContentItemId" => "item-1",
            _ => null,
        };

        Assert.True(rule.Admits(column => Row("user-1", column)));

        // Assigned to them, but owned by someone else and they may only view their own.
        Assert.False(rule.Admits(column => Row("user-2", column)));
    }
}

// Transitive scope: an item that references something invisible is itself invisible.
public class OptionSourceReferenceGuardTests
{
    private static Func<string, IReadOnlyList<string>> Graph(Dictionary<string, string[]> edges) =>
        id => edges.TryGetValue(id, out var refs) ? refs : [];

    [Fact]
    public void An_item_with_no_references_survives()
    {
        Assert.True(OptionSourceReferenceGuard.Survives("invoice-1", Graph([]), _ => true));
    }

    [Fact]
    public void An_item_whose_references_are_all_visible_survives()
    {
        var graph = Graph(new() { ["invoice-1"] = ["party-1"] });

        Assert.True(OptionSourceReferenceGuard.Survives("invoice-1", graph, _ => true));
    }

    [Fact]
    public void An_item_referencing_something_invisible_does_not_survive()
    {
        // The rule: the invoice is in violation, not merely missing a party.
        var graph = Graph(new() { ["invoice-1"] = ["party-1"] });

        Assert.False(OptionSourceReferenceGuard.Survives("invoice-1", graph, id => id != "party-1"));
    }

    [Fact]
    public void Invisibility_propagates_through_a_chain()
    {
        // A manager who cannot see the assignee must not see the task that points at
        // them, even at one remove.
        var graph = Graph(new()
        {
            ["task-1"] = ["assignment-1"],
            ["assignment-1"] = ["user-profile-1"],
        });

        Assert.False(OptionSourceReferenceGuard.Survives("task-1", graph, id => id != "user-profile-1"));
    }

    [Fact]
    public void A_cycle_terminates_instead_of_recursing_forever()
    {
        var graph = Graph(new()
        {
            ["a"] = ["b"],
            ["b"] = ["a"],
        });

        Assert.True(OptionSourceReferenceGuard.Survives("a", graph, _ => true));
    }

    [Fact]
    public void A_cycle_containing_something_invisible_still_denies()
    {
        var graph = Graph(new()
        {
            ["a"] = ["b"],
            ["b"] = ["a", "secret"],
        });

        Assert.False(OptionSourceReferenceGuard.Survives("a", graph, id => id != "secret"));
    }

    [Fact]
    public void A_chain_deeper_than_the_budget_denies_rather_than_allows()
    {
        // Fail closed: an answer that cannot be computed within budget is not
        // evidence of permission.
        var edges = new Dictionary<string, string[]>();
        for (var i = 0; i < OptionSourceReferenceGuard.MaxDepth + 3; i++)
        {
            edges[$"n{i}"] = [$"n{i + 1}"];
        }

        Assert.False(OptionSourceReferenceGuard.Survives("n0", Graph(edges), _ => true));
    }

    [Fact]
    public void Filter_keeps_only_the_surviving_items()
    {
        var graph = Graph(new()
        {
            ["invoice-1"] = ["party-1"],
            ["invoice-2"] = ["party-2"],
        });

        var kept = OptionSourceReferenceGuard.Filter(
            new[] { "invoice-1", "invoice-2" },
            id => id,
            graph,
            id => id != "party-2");

        Assert.Equal(["invoice-1"], kept);
    }
}
