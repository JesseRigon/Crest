using System.Text.Json.Nodes;
using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents.Types;
using Grove.Display.Management.Components;
using Microsoft.Extensions.Localization;
using NSubstitute;
using Grove.Services;
using Grove.Infrastructure.Settings;
using Xunit;

namespace Grove.Contents.OptionPicker.Tests;

// The id->key translation behind cascading dropdowns: a picker parent's editor state
// carries stored ids, but dependent filters compare machine data (keys, values,
// categories), so parent rows project to a comparison column - Key by default.
public class OptionParentValueProjectionTests
{
    private static OptionRow Row(string id, string? key = null, params (string Path, string? Value)[] values) =>
        new(id, values.ToDictionary(entry => entry.Path, entry => entry.Value, StringComparer.OrdinalIgnoreCase)) { Key = key };

    [Fact]
    public void Default_column_projects_the_option_key()
    {
        var values = OptionParentValueResolver.Project([Row("id-1", "US"), Row("id-2", "CA")], null);

        Assert.Equal(new[] { "US", "CA" }, values);
    }

    [Fact]
    public void Entity_rows_without_keys_fall_back_to_their_id()
    {
        // A user's or content item's identity IS its id - the identity rule.
        var values = OptionParentValueResolver.Project([Row("user-7")], "Key");

        Assert.Equal(new[] { "user-7" }, values);
    }

    [Fact]
    public void A_named_column_projects_that_column()
    {
        var values = OptionParentValueResolver.Project(
            [Row("id-1", "US", ("Value", "+1")), Row("id-2", "AG", ("Value", "+1-268"))],
            "Value");

        Assert.Equal(new[] { "+1", "+1-268" }, values);
    }

    [Fact]
    public void Blank_and_duplicate_projections_drop()
    {
        var values = OptionParentValueResolver.Project(
            [Row("a", "US"), Row("b", "us"), Row("c", "  ")],
            null);

        Assert.Equal(new[] { "US", "c" }, values);
    }
}

public class OptionDependentFilterTranslatorTests
{
    private static OptionFilter Dependent(string valueFrom, string? column = null) => new()
    {
        Path = "Category",
        Operator = OptionFilterOperators.Equals,
        ValueFrom = valueFrom,
        ValueFromColumn = column,
    };

    private static Task<IReadOnlyList<string>> FakeResolve(string path, string? column, IReadOnlyList<string> raw) =>
        Task.FromResult<IReadOnlyList<string>>([.. raw.Select(value => $"KEY({value})")]);

    [Fact]
    public async Task Static_filters_pass_through_untouched()
    {
        var filters = new[] { new OptionFilter { Path = "Category", Value = "Mass" } };
        var (rewritten, state) = await OptionDependentFilterTranslator.TranslateAsync(filters, null, FakeResolve);

        Assert.Same(filters[0], rewritten[0]);
        Assert.Null(state);
    }

    [Fact]
    public async Task A_dependent_filter_is_cloned_onto_translated_values()
    {
        var original = Dependent("Organization.Country");
        var state = new Dictionary<string, IReadOnlyList<string>> { ["Organization.Country"] = ["id-1"] };

        var (rewritten, translated) = await OptionDependentFilterTranslator.TranslateAsync([original], state, FakeResolve);

        // The stored settings object is never mutated.
        Assert.Equal("Organization.Country", original.ValueFrom);
        Assert.NotEqual("Organization.Country", rewritten[0].ValueFrom);
        Assert.Equal(new[] { "KEY(id-1)" }, translated![rewritten[0].ValueFrom!]);
        // Resolving through the pure resolver yields the translated comparison value.
        var resolution = OptionFilterResolver.Resolve(rewritten, translated);
        Assert.Equal(new[] { "KEY(id-1)" }, resolution.Filters[0].Values);
    }

    [Fact]
    public async Task An_empty_parent_stays_empty_so_RequireParentValue_still_gates()
    {
        var (rewritten, translated) = await OptionDependentFilterTranslator.TranslateAsync(
            [Dependent("Organization.Country")], null, FakeResolve);

        var resolution = OptionFilterResolver.Resolve(rewritten, translated);
        Assert.True(resolution.HasUnmetDependencies);
    }

    [Fact]
    public async Task Two_filters_reading_different_columns_of_one_parent_get_distinct_keys()
    {
        var state = new Dictionary<string, IReadOnlyList<string>> { ["Country"] = ["id-1"] };
        var (rewritten, _) = await OptionDependentFilterTranslator.TranslateAsync(
            [Dependent("Country"), Dependent("Country", "Value")], state, FakeResolve);

        Assert.NotEqual(rewritten[0].ValueFrom, rewritten[1].ValueFrom);
    }
}

// Field conditions (docs/content-engine.md › Validation and conditions run on the one expression
// engine): predicates over field paths, evaluated by the one evaluator display bindings use.
public class FieldConditionTests
{
    private static DisplayBinding Status(string @operator, string? value = null) =>
        DisplayBinding.Compare(DisplayBinding.FromField("Invoice.Status"), @operator, value is null ? null : DisplayBinding.Literal(JsonValue.Create(value)));

    private static bool IsMet(DisplayBinding condition, params string[] status) =>
        FieldConditions.IsMet(condition, path => path == "Invoice.Status" ? FieldConditions.Value(status) : null);

    [Fact]
    public void Not_empty_holds_once_the_field_has_a_value()
    {
        Assert.False(IsMet(Status(DisplayPredicates.NotEmpty)));
        Assert.True(IsMet(Status(DisplayPredicates.NotEmpty), "anything"));
    }

    [Fact]
    public void Equal_compares_the_key_and_a_field_holding_several_values_equals_each()
    {
        var voided = Status(DisplayPredicates.Equal, "Voided");

        Assert.True(IsMet(voided, "Voided"));
        Assert.True(IsMet(voided, "Posted", "Voided"));
        Assert.False(IsMet(voided, "Posted"));
        Assert.False(IsMet(voided));
    }

    [Fact]
    public void One_of_several_keys_is_an_or_of_comparisons()
    {
        var condition = DisplayBinding.Combine(DisplayPredicates.Or, [Status(DisplayPredicates.Equal, "Voided"), Status(DisplayPredicates.Equal, "Cancelled")]);

        Assert.True(IsMet(condition, "Cancelled"));
        Assert.False(IsMet(condition, "Draft"));
        Assert.Equal(["Invoice.Status", "Invoice.Status"], FieldConditions.Fields(condition));
    }

    [Fact]
    public void Stored_values_read_ids_text_and_values_by_field_path()
    {
        var content = new JsonObject
        {
            ["Invoice"] = new JsonObject
            {
                ["Customer"] = new JsonObject { ["SelectedIds"] = new JsonArray("c-1", "c-2") },
                ["Number"] = new JsonObject { ["Text"] = "EU-1" },
                ["Amount"] = new JsonObject { ["Value"] = 12 },
            },
        };

        Assert.Equal(["c-1", "c-2"], FieldConditions.StoredValues(content, "Invoice.Customer"));
        Assert.Equal(["EU-1"], FieldConditions.StoredValues(content, "Invoice.Number"));
        Assert.Equal(["12"], FieldConditions.StoredValues(content, "Invoice.Amount"));
        Assert.Empty(FieldConditions.StoredValues(content, "Invoice.Missing"));
    }

    [Fact]
    public void A_relative_path_reads_the_attachment_the_condition_is_evaluated_under()
    {
        var condition = DisplayBinding.Compare(DisplayBinding.FromField("Status"), DisplayPredicates.NotEmpty);

        Assert.Equal(["Billing.Status"], FieldConditions.Fields(FieldConditions.Qualify(condition, "Billing")));
        Assert.Equal(["Invoice.Status"], FieldConditions.Fields(FieldConditions.Qualify(Status(DisplayPredicates.NotEmpty), "Billing")));
    }

    [Fact]
    public void Literals_compared_with_a_case_insensitive_field_are_lower_cased()
    {
        var condition = FieldConditions.IgnoreCase(Status(DisplayPredicates.Equal, "Voided"), path => path == "Invoice.Status");

        Assert.True(IsMet(condition, "voided"));
        Assert.False(IsMet(Status(DisplayPredicates.Equal, "Voided"), "voided"));
    }

    [Fact]
    public void Check_refuses_what_a_field_condition_cannot_be()
    {
        Assert.Empty(FieldConditions.Check(Status(DisplayPredicates.Equal, "Voided"), _ => true));
        Assert.NotEmpty(FieldConditions.Check(Status(DisplayPredicates.Equal), _ => true));
        Assert.NotEmpty(FieldConditions.Check(Status("~"), _ => true));
        Assert.NotEmpty(FieldConditions.Check(Status(DisplayPredicates.NotEmpty), _ => false));
        Assert.NotEmpty(FieldConditions.Check(DisplayBinding.Compare(DisplayBinding.FromVariable("v"), DisplayPredicates.NotEmpty), _ => true));
    }
}

// The save side: hidden fields cleared to a fixpoint, the type's rules refusing by field path,
// with option ids compared as keys.
public class FieldConditionEnforcerTests
{
    private static JsonObject When(string field, string @operator, string? value = null)
    {
        var condition = new JsonObject { ["Field"] = field, ["Operator"] = @operator };
        if (value is not null)
        {
            condition["Operand"] = new JsonObject { ["Value"] = value };
        }

        return condition;
    }

    private static ContentFieldDefinition Field(string type, string name, JsonObject? when = null) =>
        new(new ContentFieldTypeDefinition(type), name, when is null ? [] : new JsonObject { ["FieldVisibilitySettings"] = new JsonObject { ["When"] = when } });

    private static ContentTypeDefinition Invoice(JsonArray? rules = null) => new(
        "Invoice",
        "Invoice",
        [
            new ContentTypeFieldSetDefinition("Invoice", new ContentFieldSetDefinition("Invoice",
            [
                Field("OptionListField", "Status"),
                Field("TextField", "VoidReason", When("Status", DisplayPredicates.Equal, "Voided")),
                Field("TextField", "VoidNote", When("VoidReason", DisplayPredicates.NotEmpty)),
                Field("TextField", "Approver"),
            ], []), []),
        ],
        rules is null ? [] : new JsonObject { ["FieldRulesSettings"] = new JsonObject { ["Rules"] = rules } });

    private static ContentItem Item(string statusId, string? reason = null, string? note = null, string? approver = null)
    {
        var invoice = new JsonObject { ["Status"] = new JsonObject { ["SelectedIds"] = new JsonArray(statusId) } };
        if (reason is not null) invoice["VoidReason"] = new JsonObject { ["Text"] = reason };
        if (note is not null) invoice["VoidNote"] = new JsonObject { ["Text"] = note };
        if (approver is not null) invoice["Approver"] = new JsonObject { ["Text"] = approver };

        var item = new ContentItem { ContentItemId = "i-1", ContentType = "Invoice" };
        item.Content["Invoice"] = invoice;
        return item;
    }

    private static FieldConditionEnforcer Enforcer(ContentTypeDefinition type)
    {
        var definitions = Substitute.For<IContentDefinitionManager>();
        definitions.GetTypeDefinitionAsync("Invoice").Returns(type);

        // Option ids resolve to keys, lower-cased as option keys compare: s-void is the Voided option.
        var resolver = Substitute.For<IFieldComparisonValueResolver>();
        resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<string>(1) == "Invoice.Status"
                ? new FieldComparisonValues([.. call.ArgAt<IReadOnlyList<string>>(3).Select(id => id == "s-void" ? "voided" : id)], IgnoreCase: true)
                : new FieldComparisonValues(call.ArgAt<IReadOnlyList<string>>(3)));

        var localizer = Substitute.For<IStringLocalizer<FieldConditionEnforcer>>();
        localizer[Arg.Any<string>(), Arg.Any<object[]>()].Returns(call => new LocalizedString(call.Arg<string>()!, string.Format(call.Arg<string>()!, call.Arg<object[]>()!)));
        return new FieldConditionEnforcer(definitions, [resolver], localizer);
    }

    private static JsonObject Invoice(ContentItem item) => (JsonObject)((JsonObject)(System.Text.Json.Dynamic.JsonDynamicObject)item.Content)["Invoice"]!;

    [Fact]
    public async Task A_visible_field_keeps_its_value()
    {
        var item = Item("s-void", reason: "Duplicate", note: "See EU-1");

        await Enforcer(Invoice()).ClearHiddenFieldsAsync(item, TestContext.Current.CancellationToken);

        Assert.NotNull(Invoice(item)["VoidReason"]);
        Assert.NotNull(Invoice(item)["VoidNote"]);
    }

    [Fact]
    public async Task Hidden_fields_are_cleared_to_a_fixpoint()
    {
        var item = Item("s-posted", reason: "Duplicate", note: "See EU-1");

        await Enforcer(Invoice()).ClearHiddenFieldsAsync(item, TestContext.Current.CancellationToken);

        Assert.Null(Invoice(item)["VoidReason"]);
        Assert.Null(Invoice(item)["VoidNote"]);
    }

    [Fact]
    public async Task A_rule_requires_a_field_by_its_path_while_its_condition_holds()
    {
        var rules = new JsonArray(new JsonObject { ["When"] = When("Invoice.Status", DisplayPredicates.Equal, "Voided"), ["Field"] = "Invoice.Approver", ["Effect"] = FieldRuleEffects.Require });
        var enforcer = Enforcer(Invoice(rules));

        var voided = new ContentValidateResult();
        await enforcer.ValidateRulesAsync(Item("s-void", reason: "Duplicate"), voided, TestContext.Current.CancellationToken);
        Assert.False(voided.Succeeded);
        Assert.Equal(["Invoice.Approver"], voided.Errors.Single().MemberNames);

        var posted = new ContentValidateResult();
        await enforcer.ValidateRulesAsync(Item("s-posted"), posted, TestContext.Current.CancellationToken);
        Assert.True(posted.Succeeded);

        var approved = new ContentValidateResult();
        await enforcer.ValidateRulesAsync(Item("s-void", approver: "Ann"), approved, TestContext.Current.CancellationToken);
        Assert.True(approved.Succeeded);
    }

    [Fact]
    public async Task A_hidden_field_is_exempt_from_a_rule_requiring_it()
    {
        var rules = new JsonArray(new JsonObject { ["When"] = When("Invoice.Status", DisplayPredicates.NotEmpty), ["Field"] = "Invoice.VoidReason", ["Effect"] = FieldRuleEffects.Require });

        var result = new ContentValidateResult();
        await Enforcer(Invoice(rules)).ValidateRulesAsync(Item("s-posted"), result, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task A_refusing_rule_answers_its_message_under_its_field()
    {
        var rules = new JsonArray(new JsonObject
        {
            ["When"] = When("Invoice.Status", DisplayPredicates.Equal, "Voided"),
            ["Field"] = "Invoice.Status",
            ["Effect"] = FieldRuleEffects.Refuse,
            ["Message"] = "Voiding is closed this month.",
        });

        var result = new ContentValidateResult();
        await Enforcer(Invoice(rules)).ValidateRulesAsync(Item("s-void"), result, TestContext.Current.CancellationToken);

        var error = Assert.Single(result.Errors);
        Assert.Equal("Voiding is closed this month.", error.ErrorMessage);
        Assert.Equal(["Invoice.Status"], error.MemberNames);
    }
}
