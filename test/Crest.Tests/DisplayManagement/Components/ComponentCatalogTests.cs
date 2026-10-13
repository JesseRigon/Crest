using System.Runtime.Loader;
using Crest.DisplayManagement.Components;
using Crest.DisplayManagement.SourceGenerators;
using Microsoft.AspNetCore.Components;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

#nullable enable

namespace Crest.Tests.DisplayManagement.Components;

public class ComponentCatalogTests
{
    private static readonly Assembly s_testAssembly = typeof(TestCard).Assembly;

    [Fact]
    public void ReflectionCatalogDescribesMarkedComponentsOnly()
    {
        var names = new ReflectionComponentCatalog(s_testAssembly).Describe().Select(descriptor => descriptor.Name).ToArray();

        Assert.Contains("Test__Card", names);
        Assert.Contains("Test__Plain", names);
        Assert.DoesNotContain(names, name => name.Contains("Unmarked", StringComparison.Ordinal));
    }

    [Fact]
    public void ReflectionCatalogBuildsTheSchema()
    {
        var card = new ReflectionComponentCatalog(s_testAssembly).Describe().Single(descriptor => descriptor.Name == "Test__Card");

        Assert.Equal(typeof(TestCard), card.Type);
        Assert.Equal(s_testAssembly.GetName().Name, card.Module);
        Assert.Null(card.Feature);
        Assert.Equal(new[] { "Admin", "Member", "Site" }, card.Shells.Select(shell => shell.ToString()).Order());
        Assert.Equal(("Card", "Layout", "card", "ViewCards"), (card.DisplayName, card.Category, card.Icon, card.Permission));
        Assert.Equal(ComponentInstances.Single, card.Instances);
        Assert.True(card.IsHost);
        Assert.False(card.IsWrapper);

        // Base class first; the capture-unmatched splat, cascading and plain properties are not parameters.
        Assert.Equal(
            new[] { "Style", "Title", "Count", "Amount", "Elevated", "Tone", "Size", "Since", "Tags", "Source", "Clicked" },
            card.Parameters.Select(parameter => parameter.Name));

        AssertParameter(card, "Style", ParameterKind.String, "text", group: "Appearance", advanced: true);
        AssertParameter(card, "Title", ParameterKind.String, "text", group: "Content", required: true, displayName: "Title");
        AssertParameter(card, "Count", ParameterKind.Number, "number");
        AssertParameter(card, "Amount", ParameterKind.Number, "number");
        AssertParameter(card, "Elevated", ParameterKind.Boolean, "bool");
        AssertParameter(card, "Tone", ParameterKind.Choice, "enum", choices: ["Neutral", "Warning", "Accent"]);
        AssertParameter(card, "Size", ParameterKind.Choice, "enum", choices: ["Small", "Large"]);
        AssertParameter(card, "Since", ParameterKind.String, "date", required: true);
        AssertParameter(card, "Tags", ParameterKind.Array, "list");
        AssertParameter(card, "Source", ParameterKind.Object, "picker", accepts: ["ContentItem"]);
        AssertParameter(card, "Clicked", ParameterKind.EventHandler, "action");

        Assert.Equal(typeof(decimal?), card.Parameter("amount")!.ClrType);

        var body = Assert.Single(card.Zones, zone => zone.Name == "Body");
        Assert.Equal(new[] { "Text", "Image" }, body.Allowed);
        Assert.Equal((1, 3), (body.Min, body.Max));
        Assert.True(body.Admits("image"));
        Assert.False(body.Admits("Button"));

        var footer = Assert.Single(card.Zones, zone => zone.Name == "Footer");
        Assert.Empty(footer.Allowed);
        Assert.True(footer.Admits("Anything"));
    }

    [Fact]
    public void OutputsOverlaysAndEditorsAreDescribed()
    {
        var catalog = new ReflectionComponentCatalog(s_testAssembly).Describe().ToArray();

        var list = catalog.Single(descriptor => descriptor.Name == "Test__List");
        var selected = list.Parameter("Selected");
        Assert.NotNull(selected);
        Assert.Equal(ParameterKind.Output, selected.Kind);
        Assert.Equal("output", selected.Editor);
        Assert.Equal("Selection", selected.DisplayName);
        Assert.Equal(typeof(string), selected.ValueType);
        Assert.True(selected.IsEvent);
        Assert.Equal(["Selected"], list.Outputs.Select(output => output.Name));

        var pressed = list.Parameter("Pressed");
        Assert.NotNull(pressed);
        Assert.Equal(ParameterKind.EventHandler, pressed.Kind);
        Assert.Equal(typeof(int), pressed.ValueType);

        Assert.True(catalog.Single(descriptor => descriptor.Name == "Test__Menu").IsOverlay);
        Assert.Equal(["TestField", "OtherField"], catalog.Single(descriptor => descriptor.Name == "Test__Editor").Edits);
        Assert.Empty(list.Edits);
    }

    [Fact]
    public void GeneratedCatalogMatchesTheReflectionFallback()
    {
        // Crest.Tests runs the generator, so its catalog is the generated one.
        Assert.NotNull(s_testAssembly.GetCustomAttribute<ComponentCatalogAttribute>());

        var generated = ComponentCatalogs.ForAssembly(s_testAssembly).Describe().Select(Signature).Order();
        var reflected = new ReflectionComponentCatalog(s_testAssembly).Describe().Select(Signature).Order();

        Assert.Equal(reflected, generated);
    }

    [Fact]
    public void AssemblyWithoutGeneratedCatalogUsesReflection()
    {
        var assembly = Compile("""
            using Crest.DisplayManagement.Components;
            using Microsoft.AspNetCore.Components;

            [assembly: Crest.Components.Modules.CrestShell("site")]

            namespace Crest.Components.Modules
            {
                [System.AttributeUsage(System.AttributeTargets.Assembly)]
                public sealed class CrestShellAttribute(string shell) : System.Attribute
                {
                    public string Shell { get; } = shell;
                }
            }

            namespace Sample
            {
                [CrestBlazorComponent("Banner")]
                public sealed class Banner : ComponentBase
                {
                    [Parameter] public string? Text { get; set; }
                }
            }
            """, "Sample.NoGenerator", runGenerator: false);

        Assert.Null(assembly.GetCustomAttribute<ComponentCatalogAttribute>());

        var banner = Assert.Single(ComponentCatalogs.ForAssembly(assembly).Describe());
        Assert.Equal("Banner", banner.Name);
        Assert.Equal(new[] { ShellKind.Site }, banner.Shells);
        Assert.Equal("Sample.NoGenerator", banner.Module);
        Assert.Equal("Text", Assert.Single(banner.Parameters).Name);
    }

    [Fact]
    public void GeneratorEmitsTheCatalogAndTheAssemblyAttribute()
    {
        const string Source = """
            using Crest.DisplayManagement.Components;
            using Microsoft.AspNetCore.Components;

            [assembly: Crest.Components.Modules.CrestShell("member")]

            namespace Crest.Components.Modules
            {
                [System.AttributeUsage(System.AttributeTargets.Assembly)]
                public sealed class CrestShellAttribute(string shell) : System.Attribute
                {
                    public string Shell { get; } = shell;
                }
            }

            namespace Sample
            {
                public enum Level { Low, High }

                public class PanelBase<T> : ComponentBase
                {
                    [Parameter] public T? Value { get; set; }
                }

                [CrestBlazorComponent("Panel", DisplayName = "Panel \"quoted\"", Category = "Layout", IsWrapper = true)]
                public sealed partial class Panel : PanelBase<int>
                {
                    [Parameter, ComponentParameter(Required = true, Group = "Content")] public string? Heading { get; set; }
                    [Parameter] public Level Level { get; set; }
                    [Parameter] public bool? Open { get; set; }
                    [Parameter] public Nested? Options { get; set; }
                    [Parameter, ComponentZone(Allowed = ["Text"], Max = 1)] public RenderFragment? ChildContent { get; set; }
                    [Parameter] public EventCallback Closed { get; set; }

                    public sealed class Nested
                    {
                    }
                }

                [CrestBlazorComponent("Generic")]
                public sealed class Generic<T> : ComponentBase
                {
                }

                internal sealed class Hidden
                {
                    [CrestBlazorComponent("Inner")]
                    internal sealed class Inner : ComponentBase
                    {
                        [Parameter] public string? Text { get; set; }
                    }
                }
            }
            """;

        var (compilation, diagnostics, generated) = RunGenerator(Source, "Sample.Member.BlazorWasm");

        Assert.Empty(compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == ComponentCatalogGenerator.SkippedComponentDiagnosticId && diagnostic.GetMessage().Contains("Generic", StringComparison.Ordinal));
        Assert.Contains("[assembly: global::Crest.DisplayManagement.Components.ComponentCatalogAttribute(", generated, StringComparison.Ordinal);
        Assert.Contains("ComponentCatalog_Sample_Member_BlazorWasm", generated, StringComparison.Ordinal);

        var assembly = Load(compilation);
        var catalog = ComponentCatalogs.ForAssembly(assembly).Describe().ToArray();

        Assert.Equal(new[] { "Inner", "Panel" }, catalog.Select(descriptor => descriptor.Name).Order());

        var panel = catalog.Single(descriptor => descriptor.Name == "Panel");
        Assert.Equal("Panel \"quoted\"", panel.DisplayName);
        Assert.True(panel.IsWrapper);
        Assert.Equal(new[] { ShellKind.Member }, panel.Shells);
        Assert.Equal("Sample.Member.BlazorWasm", panel.Module);
        Assert.Equal(new[] { "Value", "Heading", "Level", "Open", "Options", "Closed" }, panel.Parameters.Select(parameter => parameter.Name));
        Assert.Equal(typeof(int), panel.Parameter("Value")!.ClrType);
        Assert.Equal(ParameterKind.Number, panel.Parameter("Value")!.Kind);
        Assert.True(panel.Parameter("Heading")!.Required);
        Assert.Equal(new[] { "Low", "High" }, panel.Parameter("Level")!.Choices);
        Assert.Equal(ParameterKind.Boolean, panel.Parameter("Open")!.Kind);
        Assert.Equal(ParameterKind.Object, panel.Parameter("Options")!.Kind);
        Assert.Equal(ParameterKind.EventHandler, panel.Parameter("Closed")!.Kind);
        Assert.Equal(Signature(new ZoneDescriptor("ChildContent", ["Text"], 0, 1)), Signature(Assert.Single(panel.Zones)));

        // Same descriptors as the reflection fallback over the same assembly.
        Assert.Equal(
            new ReflectionComponentCatalog(assembly).Describe().Select(Signature).Order(),
            catalog.Select(Signature).Order());
    }

    [Fact]
    public void GeneratorReportsParametersLeftInRazorMarkup()
    {
        const string Source = """
            using Crest.DisplayManagement.Components;
            using Microsoft.AspNetCore.Components;

            namespace Sample
            {
                [CrestBlazorComponent("Badge")]
                public partial class Badge : ComponentBase
                {
                }
            }
            """;

        const string Razor = """
            <span>@Text</span>
            @code {
                [Parameter] public string? Text { get; set; }
            }
            """;

        var (_, diagnostics, _) = RunGenerator(Source, "Sample", ("/src/Badge.razor", Razor), ("/src/Marked.razor", "@attribute [CrestBlazorComponent(\"Marked\")]\n<p></p>"));

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == ComponentCatalogGenerator.MarkupParameterDiagnosticId);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == ComponentCatalogGenerator.MarkupMarkerDiagnosticId);
    }

    [Fact]
    public void GeneratorEmitsNothingWithoutComponents()
    {
        var (_, diagnostics, generated) = RunGenerator("namespace Sample { public sealed class Plain { } }", "Sample");

        Assert.Empty(diagnostics);
        Assert.Equal(string.Empty, generated);
    }

    private static void AssertParameter(
        ComponentDescriptor component,
        string name,
        ParameterKind kind,
        string editor,
        string? group = null,
        bool required = false,
        bool advanced = false,
        string? displayName = null,
        string[]? choices = null,
        string[]? accepts = null)
    {
        var parameter = component.Parameter(name);
        Assert.NotNull(parameter);
        Assert.Equal(kind, parameter.Kind);
        Assert.Equal(editor, parameter.Editor);
        Assert.Equal(group, parameter.Group);
        Assert.Equal(required, parameter.Required);
        Assert.Equal(advanced, parameter.Advanced);
        Assert.Equal(displayName, parameter.DisplayName);
        Assert.Equal(choices, parameter.Choices);
        Assert.Equal(accepts, parameter.Accepts);
    }

    // Everything a descriptor says, as one comparable string (records compare lists by reference).
    internal static string Signature(ComponentDescriptor descriptor) => string.Join(" | ",
        descriptor.Name,
        descriptor.Type?.FullName,
        descriptor.Module,
        descriptor.Feature,
        descriptor.Permission,
        descriptor.Instances,
        descriptor.IsWrapper,
        descriptor.IsHost,
        descriptor.DisplayName,
        descriptor.Category,
        descriptor.Icon,
        descriptor.IsOverlay,
        string.Join(",", descriptor.Edits),
        string.Join(",", descriptor.Shells.Order()),
        string.Join(";", descriptor.Parameters.Select(Signature)),
        string.Join(";", descriptor.Zones.Select(Signature)));

    private static string Signature(ParameterDescriptor parameter) =>
        $"{parameter.Name}:{parameter.ClrType}:{parameter.Kind}:{parameter.Editor}:{parameter.DisplayName}:{parameter.Group}:{parameter.Required}:{parameter.Hidden}:{parameter.Advanced}:{parameter.ReadOnly}:{Join(parameter.Choices)}:{Join(parameter.Accepts)}:{parameter.ValueType}";

    private static string Signature(ZoneDescriptor zone) => $"{zone.Name}:{Join(zone.Allowed)}:{zone.Min}:{zone.Max}";

    private static string Join(IReadOnlyList<string>? values) => values is null ? "null" : string.Join(",", values);

    private static (Compilation Compilation, IReadOnlyList<Diagnostic> Diagnostics, string Generated) RunGenerator(
        string source,
        string assemblyName,
        params (string Path, string Text)[] razorFiles)
    {
        var compilation = CreateCompilation(source, assemblyName);
        var driver = CSharpGeneratorDriver.Create(
            generators: [new ComponentCatalogGenerator().AsSourceGenerator()],
            additionalTexts: razorFiles.Select(file => (AdditionalText)new InMemoryText(file.Path, file.Text)),
            parseOptions: (CSharpParseOptions)compilation.SyntaxTrees.First().Options);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics, TestContext.Current.CancellationToken);
        var generated = string.Concat(output.SyntaxTrees.Skip(1).Select(tree => tree.ToString()));

        return (output, diagnostics, generated);
    }

    private static Assembly Compile(string source, string assemblyName, bool runGenerator)
    {
        var compilation = runGenerator ? RunGenerator(source, assemblyName).Compilation : CreateCompilation(source, assemblyName);
        Assert.Empty(compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        return Load(compilation);
    }

    private static Assembly Load(Compilation compilation)
    {
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        Assert.True(result.Success, string.Join(System.Environment.NewLine, result.Diagnostics));
        stream.Position = 0;

        return new AssemblyLoadContext(compilation.AssemblyName, isCollectible: true).LoadFromStream(stream);
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var trusted = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        var references = trusted
            .Append(typeof(ComponentBase).Assembly.Location)
            .Append(typeof(IComponentCatalog).Assembly.Location)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path));

        return CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source, parseOptions)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }

    private sealed class InMemoryText(string path, string text) : AdditionalText
    {
        public override string Path { get; } = path;

        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(text);
    }
}
