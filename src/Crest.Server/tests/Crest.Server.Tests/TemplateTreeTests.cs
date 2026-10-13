using System.Text.Json.Nodes;
using Crest.DisplayManagement.Components;
using Crest.DisplayManagement.Theming;
using Crest.Environment.Extensions;
using Crest.Infrastructure.Html;
using Crest.Templates.Models;
using Crest.Templates.Services;
using Fluid;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Crest.Server.Tests;

// Templates as Liquid that emits display-tree nodes (docs/blazor-display.md § 4a, § 5, rework
// step 4), evaluated under the render limits (rulings 2026-10-12).
public sealed class TemplateTreeTests
{
    [Fact]
    public async Task AComponentTagIsANodeWithItsParametersCheckedAgainstTheSchema()
    {
        var evaluation = await EvaluateAsync("""{% component "Label", text: "Hello", count: "3", colour: "red" %}""");

        var node = evaluation.Root;
        Assert.Equal("Label", node.Component);
        Assert.Equal("Hello", node.Properties["Text"]!.GetValue<string>());
        Assert.Equal(3, node.Properties["Count"]!.GetValue<int>());
        Assert.Contains(evaluation.Diagnostics, diagnostic => diagnostic.Reason.Contains("'colour' is not a parameter", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ComposeZonesTextAndChildrenBuildOneTree()
    {
        var evaluation = await EvaluateAsync("""
            {% compose "Card" %}
              {% zone "Header" %}<h2>Title <script>alert(1)</script></h2>{% endzone %}
              {% component "Label", text: Model.Name %}
              {% children "Content" %}
            {% endcompose %}
            """, model: new Dictionary<string, object?> { ["Name"] = "From model" });

        var card = evaluation.Root;
        Assert.Equal("Card", card.Component);

        var header = Assert.Single(card.Zones["Header"]);
        Assert.Equal(DisplayNode.MarkupComponent, header.Component);
        Assert.DoesNotContain("script", header.Properties[DisplayNode.MarkupProperty]!.GetValue<string>(), StringComparison.Ordinal);

        var body = card.Zones["ChildContent"];
        Assert.Equal("From model", body[0].Properties["Text"]!.GetValue<string>());
        Assert.Equal(DisplayNode.ShapeZoneComponent, body[1].Component);
        Assert.Equal("Content", body[1].Properties[DisplayNode.ShapeZoneProperty]!.GetValue<string>());
    }

    [Fact]
    public async Task BindingsEventsAndActionsAreWiredByScopedIds()
    {
        var evaluation = await EvaluateAsync("""
            {% compose "List", id: "invoices" %}
              {% on "row_menu" %}{% set_variable "row", event.item %}{% open "rowMenu" %}{% endon %}
              {% on "selected" %}{% run_workflow "post-invoice", into: "result", invoice: event.id %}{% endon %}
            {% endcompose %}
            {% component "Label", id: "details", text: nodes.invoices.selected.name %}
            {% compose "Overlay", id: "rowMenu" %}{% component "Label", text: page.row.name %}{% endcompose %}
            """, instanceId: "page");

        var nodes = evaluation.Root.Zones["ChildContent"];
        var list = nodes[0];
        Assert.Equal("page.invoices", list.Id);
        Assert.Equal([DisplayActionKind.Set, DisplayActionKind.Open], list.Events!["RowMenu"].Select(action => action.Kind));
        Assert.Equal("page.rowMenu", list.Events["RowMenu"][1].Target);
        Assert.True(list.Events["RowMenu"][0].Value!.Event);
        Assert.Equal("item", list.Events["RowMenu"][0].Value!.Path);

        var workflow = Assert.Single(list.Events["Selected"]);
        Assert.Equal(("post-invoice", "result"), (workflow.Workflow, workflow.Target));
        Assert.Equal("id", workflow.Arguments!["invoice"].Path);

        var binding = nodes[1].Bindings!["Text"];
        Assert.Equal(("page.invoices", "Selected", "name"), (binding.Node, binding.Output, binding.Path));

        var menuLabel = Assert.Single(nodes[2].Zones["ChildContent"]);
        Assert.Equal(("row", "name"), (menuLabel.Bindings!["Text"].Variable, menuLabel.Bindings["Text"].Path));

        Assert.Empty(DisplayTreeValidator.Validate(evaluation.Root, Registry()));
    }

    [Fact]
    public async Task AStaticSlotIsReadWhileBuildingAndABoundOneIsLeftToTheBrowser()
    {
        var source = Substitute.For<IDisplayDataSource>();
        source.QueryAsync("Recent", Arg.Any<IReadOnlyDictionary<string, JsonNode?>>(), Arg.Any<CancellationToken>())
            .Returns(new JsonObject { ["items"] = new JsonArray(new JsonObject { ["name"] = "Acme" }) });

        var evaluation = await EvaluateAsync("""
            {% compose "Label", id: "static" %}{% slot "text", "Recent", path: "items[0].name" %}{% endcompose %}
            {% compose "Label", id: "bound" %}{% slot "text", "Customer", id: nodes.static.selected %}{% endcompose %}
            """, source: source);

        var nodes = evaluation.Root.Zones["ChildContent"];
        Assert.Equal("Acme", nodes[0].Properties["Text"]!.GetValue<string>());
        Assert.False(nodes[1].Properties.ContainsKey("Text"));
        Assert.False(nodes[1].Slots!["Text"].IsStatic);
        await source.DidNotReceive().QueryAsync("Customer", Arg.Any<IReadOnlyDictionary<string, JsonNode?>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ADeclaredCompositeChecksItsInputsAndFillsDefaults()
    {
        var catalog = CatalogOf(new TemplateRecord("c", "v", "Banner", null, null, """{% component "Label", text: Model.Title, count: Model.Size %}""", null, true, true, null)
        {
            IsComposite = true,
            Inputs =
            [
                new TemplateInput { Name = "Title", Type = "string", Required = true },
                new TemplateInput { Name = "Size", Type = "number", Default = 4 },
                new TemplateInput { Name = "Tone", Type = "choice", Choices = ["Calm", "Loud"] },
            ],
        });

        var evaluation = await EvaluateAsync("""{% component "Banner", title: "Hi", tone: "Shouty", colour: "red" %}{% component "Banner" %}""", catalog: catalog);

        var first = evaluation.Root.Zones["ChildContent"][0];
        Assert.Equal(("Hi", 4), (first.Properties["Text"]!.GetValue<string>(), first.Properties["Count"]!.GetValue<int>()));
        var reasons = evaluation.Diagnostics.Select(diagnostic => diagnostic.Reason).ToArray();
        Assert.Contains(reasons, reason => reason.Contains("'colour' is not an input of the composite 'Banner'", StringComparison.Ordinal));
        Assert.Contains(reasons, reason => reason.Contains("'Tone' must be one of Calm, Loud", StringComparison.Ordinal));
        Assert.Contains(reasons, reason => reason.Contains("needs its input 'Title'", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ACompositeRepublishesItsDeclaredOutputsUnderItsInstance()
    {
        var catalog = CatalogOf(new TemplateRecord("c", "v", "Picker", null, null, """{% component "List", id: "list" %}""", null, true, true, null)
        {
            IsComposite = true,
            Outputs = [new TemplateOutput { Name = "Chosen", Node = "list", Output = "selected" }],
        });

        var evaluation = await EvaluateAsync("""
            {% component "Picker", id: "picker" %}
            {% component "Label", text: nodes.picker.chosen.name %}
            """, instanceId: "p", catalog: catalog);

        var picker = evaluation.Root.Zones["ChildContent"][0];
        Assert.Equal("p.picker", picker.Id);
        Assert.Equal(("p.picker.list", "Selected"), (picker.OutputAliases!["Chosen"].Node, picker.OutputAliases["Chosen"].Output));
        Assert.Empty(DisplayTreeValidator.Validate(evaluation.Root, Registry()));

        var state = new DisplayState();
        foreach (var (output, source) in picker.OutputAliases)
        {
            state.RegisterAlias(picker.Id, output, source);
        }

        state.SetOutput("p.picker.list", "Selected", new { name = "Ada" });
        Assert.Equal("Ada", state.Read(evaluation.Root.Zones["ChildContent"][1].Bindings!["Text"])!.GetValue<string>());
    }

    [Fact]
    public async Task TheValidatorChecksACompositesOutputsAndSamplesItsInputs()
    {
        var services = Services(Registry());
        var validator = new TemplateValidator(services.GetRequiredService<TemplateTreeEvaluator>(), Registry(), []);

        var diagnostics = await validator.ValidateAsync(new Crest.Templates.Models.TemplatePart
        {
            Name = "Picker",
            Source = """{% component "List", id: "list" %}{% component "Label", text: Model.Title %}""",
            IsComposite = true,
            Inputs = [new TemplateInput { Name = "Title", Type = "string", Required = true }],
            Outputs = [new TemplateOutput { Name = "Chosen", Node = "list", Output = "selected" }, new TemplateOutput { Name = "Gone", Node = "nowhere", Output = "selected" }, new TemplateOutput { Name = "Wrong", Node = "list", Output = "row_menu" }],
        }, TestContext.Current.CancellationToken);

        var reasons = diagnostics.Select(diagnostic => diagnostic.Reason).ToArray();
        Assert.Equal(2, reasons.Length);
        Assert.Contains(reasons, reason => reason.Contains("names the node 'nowhere'", StringComparison.Ordinal));
        Assert.Contains(reasons, reason => reason.Contains("'row_menu' is not an output", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AVariantPlacesItsBaseWithInheritedValuesUnderTheTagsOwn()
    {
        var overrides = new ComponentOverridesDocument
        {
            Variants =
            [
                new ComponentVariant { Name = "BigLabel", Base = "Label", Values = new JsonObject { ["Count"] = 9, ["Text"] = "base text" } },
                new ComponentVariant { Name = "LoudLabel", Base = "BigLabel", Values = new JsonObject { ["Text"] = "LOUD" } },
            ],
        };

        var evaluation = await EvaluateAsync("""{% component "LoudLabel" %}{% component "LoudLabel", text: "mine" %}""", overrides: overrides);

        var nodes = evaluation.Root.Zones["ChildContent"];
        Assert.All(nodes, node => Assert.Equal("Label", node.Component));
        Assert.Equal(("LOUD", 9), (nodes[0].Properties["Text"]!.GetValue<string>(), nodes[0].Properties["Count"]!.GetValue<int>()));
        Assert.Equal("mine", nodes[1].Properties["Text"]!.GetValue<string>());
    }

    [Fact]
    public void AVariantThatInheritsFromItselfDoesNotResolve()
    {
        var document = new ComponentOverridesDocument
        {
            Variants = [new ComponentVariant { Name = "A", Base = "B" }, new ComponentVariant { Name = "B", Base = "A" }],
        };

        Assert.Null(ComponentOverrides.Resolve(document, "A", out var problem));
        Assert.Contains("inherits from itself", problem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InstanceOverridesSetLiteralValuesButNeverDataDerivedOnes()
    {
        var document = new ComponentOverridesDocument
        {
            Instances = [new InstanceOverride { InstanceId = "label", Values = new JsonObject { ["Text"] = "overridden", ["Count"] = 2 } }],
        };
        var overrides = new ComponentOverrides(Documents(document), Registry());
        var plain = new DisplayNode { Id = "label", Component = "Label", Properties = new(StringComparer.OrdinalIgnoreCase) { ["Text"] = "original" } };
        var bound = new DisplayNode { Id = "label", Component = "Label", Bindings = new() { ["Text"] = DisplayBinding.FromVariable("x") } };

        await overrides.ApplyAsync(plain, TestContext.Current.CancellationToken);
        await overrides.ApplyAsync(bound, TestContext.Current.CancellationToken);

        Assert.Equal(("overridden", 2), (plain.Properties["Text"]!.GetValue<string>(), plain.Properties["Count"]!.GetValue<int>()));
        Assert.False(bound.Properties.ContainsKey("Text"));
        Assert.Equal(2, bound.Properties["Count"]!.GetValue<int>());
    }

    [Fact]
    public void OverrideValuesAreCheckedAgainstTheSchema()
    {
        Registry().TryGet("Label", out var label);

        Assert.Empty(ComponentOverrides.Check(label, new JsonObject { ["Text"] = "x", ["Count"] = "3" }));
        Assert.NotEmpty(ComponentOverrides.Check(label, new JsonObject { ["Colour"] = "red" }));
        Registry().TryGet("List", out var list);
        Assert.Contains(ComponentOverrides.Check(list, new JsonObject { ["Selected"] = "x" }), error => error.Contains("events are wired", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnUnknownComponentIsLeftOutAndDiagnosed()
    {
        var evaluation = await EvaluateAsync("""{% component "Nope" %}{% component "Label", text: "kept" %}""");

        Assert.Equal("Label", evaluation.Root.Component);
        Assert.Contains(evaluation.Diagnostics, diagnostic => diagnostic.Reason.Contains("'Nope' is neither a registered component nor a published template", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ACompositeIsEvaluatedOnItsOwnWithOnlyItsArguments()
    {
        var catalog = Catalog(("Header", null, null, """{% component "Label", text: Model.Title %}{{ Secret }}"""));

        var evaluation = await EvaluateAsync("""{% assign Secret = "leak" %}{% component "Header", title: "Hi" %}""", catalog: catalog);

        Assert.Equal("Hi", evaluation.Root.Properties["Text"]!.GetValue<string>());
        Assert.DoesNotContain(DisplayTreeValidator.Walk(evaluation.Root), node => node.Component == DisplayNode.MarkupComponent);
    }

    [Fact]
    public async Task ACompositeThatPlacesItselfStopsAsACycle()
    {
        var catalog = Catalog(("Loop", null, null, """{% component "Loop" %}"""));

        var evaluation = await EvaluateAsync("""{% component "Loop" %}""", catalog: catalog);

        Assert.Contains(DisplayTreeValidator.Walk(evaluation.Root), node => node.Component == DisplayNode.ErrorComponent && node.Diagnostics!.Any(diagnostic => diagnostic.Action == "cycle"));
    }

    [Fact]
    public async Task AnEvaluationOverItsStepBudgetBecomesAnErrorNode()
    {
        var evaluation = await EvaluateAsync(
            """{% for i in (1..100000) %}{% assign x = i %}{% endfor %}""",
            options: new DisplayTreeOptions { MaxTemplateSteps = 1_000 });

        Assert.Equal(DisplayNode.ErrorComponent, evaluation.Root.Component);
        Assert.Contains(evaluation.Diagnostics, diagnostic => diagnostic.Action is "limit" or "error");
    }

    [Fact]
    public async Task AnEvaluationOverItsNodeBudgetBecomesAnErrorNode()
    {
        var evaluation = await EvaluateAsync(
            """{% for i in (1..50) %}{% component "Label", text: "x" %}{% endfor %}""",
            options: new DisplayTreeOptions { MaxNodes = 10 });

        Assert.Equal(DisplayNode.ErrorComponent, evaluation.Root.Component);
        Assert.Contains(evaluation.Diagnostics, diagnostic => diagnostic.Action == "limit");
    }

    [Fact]
    public async Task AParseErrorIsAnErrorNode()
    {
        var evaluation = await EvaluateAsync("""{% compose "Card" %}never closed""");

        Assert.Equal(DisplayNode.ErrorComponent, evaluation.Root.Component);
        Assert.Contains(evaluation.Diagnostics, diagnostic => diagnostic.Action == "parse");
    }

    [Fact]
    public async Task TheValidatorRefusesWiringToUnknownOutputs()
    {
        var services = Services(Registry());
        var validator = new TemplateValidator(services.GetRequiredService<TemplateTreeEvaluator>(), Registry(), []);

        var diagnostics = await validator.ValidateAsync("Content", """
            {% component "List", id: "list" %}
            {% component "Label", text: nodes.list.missing %}
            """, TestContext.Current.CancellationToken);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Reason.Contains("'Missing' is not an output", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheResolverPrefersTheMostSpecificNameThenTheMostQualifiedTemplate()
    {
        var catalog = Catalog(
            ("Content", null, null, "generic"),
            ("Content__Article", null, null, "article"),
            ("Content__Article", "SiteTheme", null, "article on the theme"),
            ("Content__Article", "SiteTheme", "Summary", "article summary on the theme"),
            ("Content__Article", "OtherTheme", "Detail", "elsewhere"));
        var resolver = new TemplateResolver(catalog, Theme("SiteTheme"));
        var names = TemplateResolver.NamesOf("Content", ["Content__Article"]);

        Assert.Equal(["Content__Article", "Content"], names);
        Assert.Equal("article summary on the theme", (await resolver.ResolveAsync(names, "Summary"))!.Source);
        Assert.Equal("article on the theme", (await resolver.ResolveAsync(names, "Detail"))!.Source);
        Assert.Equal("generic", (await resolver.ResolveAsync(["Content__Page", "Content"], "Detail"))!.Source);
        Assert.Null(await new TemplateResolver(catalog, Theme("SiteTheme")).ResolveAsync(["Menu"], null));
    }

    private static async Task<TemplateEvaluation> EvaluateAsync(
        string template,
        object? model = null,
        string instanceId = "t",
        ITemplateCatalog? catalog = null,
        DisplayTreeOptions? options = null,
        IDisplayDataSource? source = null,
        ComponentOverridesDocument? overrides = null)
    {
        var services = Services(Registry(), catalog, options, source, overrides);
        return await services.GetRequiredService<TemplateTreeEvaluator>().EvaluateAsync(template, model, instanceId, cancellationToken: TestContext.Current.CancellationToken);
    }

    private static ServiceProvider Services(IComponentRegistry registry, ITemplateCatalog? catalog = null, DisplayTreeOptions? options = null, IDisplayDataSource? source = null, ComponentOverridesDocument? overrides = null)
    {
        var templateOptions = new TemplateOptions();

        var sanitizer = Substitute.For<IHtmlSanitizerService>();
        sanitizer.Sanitize(Arg.Any<string>()).Returns(call => System.Text.RegularExpressions.Regex.Replace(call.Arg<string>() ?? string.Empty, "<script.*?</script>", string.Empty));

        var services = new ServiceCollection();
        services.AddSingleton(registry);
        services.AddSingleton(sanitizer);
        services.AddSingleton(catalog ?? Catalog());
        services.AddSingleton(Theme("SiteTheme"));
        services.AddSingleton<TemplateResolver>();
        if (source is not null)
        {
            services.AddSingleton(source);
        }

        services.AddSingleton(new TemplateTreeParser(Options.Create(new FluidParserOptions { AllowFunctions = true })));
        services.AddSingleton<IMemoryCache>(new MemoryCache(new MemoryCacheOptions()));
        services.AddSingleton(Options.Create(templateOptions));
        services.AddSingleton(Options.Create(options ?? new DisplayTreeOptions()));
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>));
        services.AddScoped<TemplateTreeEvaluator>();
        services.AddSingleton(new ComponentOverrides(Documents(overrides ?? new ComponentOverridesDocument()), registry));
        return services.BuildServiceProvider();
    }

    private static IThemeManager Theme(string id)
    {
        var theme = Substitute.For<IExtensionInfo>();
        theme.Id.Returns(id);
        var themes = Substitute.For<IThemeManager>();
        themes.GetThemeAsync().Returns(theme);
        return themes;
    }

    private static ITemplateCatalog Catalog(params (string Name, string? Theme, string? DisplayType, string Source)[] templates)
    {
        var catalog = Substitute.For<ITemplateCatalog>();
        catalog.ListPublishedAsync().Returns(templates
            .Select((template, index) => new TemplateRecord($"id{index}", $"v{index}", template.Name, template.Theme, template.DisplayType, template.Source, null, true, true, null))
            .ToArray());
        return catalog;
    }

    private static ITemplateCatalog CatalogOf(params TemplateRecord[] records)
    {
        var catalog = Substitute.For<ITemplateCatalog>();
        catalog.ListPublishedAsync().Returns(records);
        return catalog;
    }

    private static Crest.Documents.IDocumentManager<ComponentOverridesDocument> Documents(ComponentOverridesDocument document)
    {
        var documents = Substitute.For<Crest.Documents.IDocumentManager<ComponentOverridesDocument>>();
        documents.GetOrCreateImmutableAsync(Arg.Any<Func<Task<ComponentOverridesDocument>>>()).Returns(document);
        documents.GetOrCreateMutableAsync(Arg.Any<Func<Task<ComponentOverridesDocument>>>()).Returns(document);
        return documents;
    }

    private static IComponentRegistry Registry() => new ComponentRegistry([new ListCatalog(
    [
        new ComponentDescriptor("Fragment", typeof(ComponentBase), [], [new ZoneDescriptor("ChildContent", [])], ComponentSchema.AllShells),
        new ComponentDescriptor(
            "Label",
            typeof(ComponentBase),
            [new ParameterDescriptor("Text", typeof(string), ParameterKind.String, "text"), new ParameterDescriptor("Count", typeof(int), ParameterKind.Number, "number")],
            [],
            ComponentSchema.AllShells),
        new ComponentDescriptor("Card", typeof(ComponentBase), [], [new ZoneDescriptor("ChildContent", []), new ZoneDescriptor("Header", [])], ComponentSchema.AllShells),
        new ComponentDescriptor(
            "List",
            typeof(ComponentBase),
            [
                new ParameterDescriptor("Selected", typeof(EventCallback<JsonNode?>), ParameterKind.Output, "output", ValueType: typeof(JsonNode)),
                new ParameterDescriptor("RowMenu", typeof(EventCallback<JsonNode?>), ParameterKind.EventHandler, "action", ValueType: typeof(JsonNode)),
            ],
            [],
            ComponentSchema.AllShells),
        new ComponentDescriptor("Overlay", typeof(ComponentBase), [], [new ZoneDescriptor("ChildContent", [])], ComponentSchema.AllShells, IsOverlay: true),
    ])]);

    private sealed class ListCatalog(IEnumerable<ComponentDescriptor> components) : IComponentCatalog
    {
        public IEnumerable<ComponentDescriptor> Describe() => components;
    }
}
