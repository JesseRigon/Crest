using System.Text.Json.Nodes;
using Grove.Display.Rules;
using Grove.Display.Rules.Models;
using Grove.Display.Rules.Services;
using Grove.Scripting;
using Grove.Scripting.JavaScript;

namespace Grove.Tests.Modules.Grove.Rules;

public class RuleConditionCheckerTests
{
    [Theory]
    [InlineData("if (true")] // Unbalanced parenthesis.
    [InlineData("return true; )")] // Stray token.
    public async Task CheckScript_WhenScriptCannotBeParsed_ReportsTheParseError(string script)
    {
        var problem = await CreateChecker().CheckScriptAsync(script);

        Assert.NotNull(problem);
        Assert.StartsWith("The script couldn't be parsed.", problem);
    }

    [Fact]
    public async Task CheckScript_WhenScriptThrows_ReportsTheException()
    {
        var problem = await CreateChecker().CheckScriptAsync("throw new Error('nope');");

        Assert.NotNull(problem);
        Assert.StartsWith("JavaScript evaluation resulted in an exception.", problem);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CheckScript_WhenScriptIsEmpty_AsksForOne(string script)
    {
        Assert.Equal("Please provide a script.", await CreateChecker().CheckScriptAsync(script));
    }

    [Fact]
    public async Task CheckScript_WhenScriptIsValid_ReportsNothing()
    {
        Assert.Null(await CreateChecker().CheckScriptAsync("return true;"));
    }

    [Fact]
    public async Task Check_ReportsEachProblemUnderItsCondition_GroupsIncluded()
    {
        var conditions = new JsonArray
        {
            new JsonObject { ["Name"] = nameof(BooleanCondition), ["ConditionId"] = "ok", ["Value"] = true },
            new JsonObject { ["Name"] = "MissingCondition", ["ConditionId"] = "unknown" },
            new JsonObject
            {
                ["Name"] = nameof(AnyConditionGroup),
                ["ConditionId"] = "group",
                ["Conditions"] = new JsonArray
                {
                    new JsonObject { ["Name"] = nameof(JavascriptCondition), ["ConditionId"] = "script", ["Script"] = "if (true" },
                    new JsonObject { ["Name"] = nameof(UrlCondition), ["ConditionId"] = "url", ["Value"] = "/blog" },
                },
            },
        };

        var errors = await CreateChecker().CheckAsync(conditions);

        Assert.Equal(["script", "unknown", "url"], errors.Keys.Order(StringComparer.Ordinal));
    }

    private static RuleConditionChecker CreateChecker()
    {
        var services = new ServiceCollection()
            .AddMemoryCache()
            .AddScripting()
            .AddJavaScriptEngine();

        var serviceProvider = services.BuildServiceProvider();

        var evaluator = new JavascriptConditionEvaluator(
            serviceProvider.GetRequiredService<IScriptingManager>(),
            serviceProvider);

        IConditionFactory[] factories =
        [
            new ConditionFactory<BooleanCondition>(),
            new ConditionFactory<AnyConditionGroup>(),
            new ConditionFactory<JavascriptCondition>(),
            new ConditionFactory<UrlCondition>(),
        ];

        return new RuleConditionChecker(factories, evaluator, new StubStringLocalizer<RuleConditionChecker>());
    }

    private sealed class StubStringLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
