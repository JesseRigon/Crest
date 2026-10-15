using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

#nullable enable

namespace Grove.Display.Management.SourceGenerators;

/// <summary>
/// Emits an assembly's component catalog (docs/blazor-display.md § 1): for every class marked
/// <c>[BlazorComponent]</c>, a <c>ComponentDescriptor</c> built from its <c>[Parameter]</c>
/// properties (kind and editor inferred from the CLR type, refined by <c>[ComponentParameter]</c>
/// and <c>[ComponentZone]</c>), the assembly's shells from <c>[assembly: GroveShell]</c> (every
/// shell without one) and the assembly name as the module; then
/// <c>[assembly: ComponentCatalog(typeof(...))]</c> so the registries find it from metadata.
/// The inference mirrors <c>ComponentSchema.Infer</c>, which the reflection fallback uses.
/// </summary>
/// <remarks>
/// The generator reads C# declarations only: Razor compiles <c>.razor</c> files with its own
/// source generator, whose output other generators never see. A component is marked, and its
/// parameters declared, in its code-behind (<c>.razor.cs</c>); OCSG003 and OCSG004 report a
/// marker or parameters left in Razor markup.
/// </remarks>
[Generator]
public sealed class ComponentCatalogGenerator : IIncrementalGenerator
{
    public const string MarkupMarkerDiagnosticId = "OCSG003";
    public const string MarkupParameterDiagnosticId = "OCSG004";
    public const string SkippedComponentDiagnosticId = "OCSG005";

    private const string MarkerAttributeName = "Grove.Display.Management.Components.BlazorComponentAttribute";
    private const string CatalogInterfaceName = "Grove.Display.Management.Components.IComponentCatalog";
    private const string ParameterRefinementName = "Grove.Display.Management.Components.ComponentParameterAttribute";
    private const string ZoneRefinementName = "Grove.Display.Management.Components.ComponentZoneAttribute";
    private const string OutputRefinementName = "Grove.Display.Management.Components.ComponentOutputAttribute";
    private const string ShellAttributeName = "Grove.Display.Components.Modules.GroveShellAttribute";
    private const string ParameterAttributeName = "Microsoft.AspNetCore.Components.ParameterAttribute";
    private const string EditorRequiredAttributeName = "Microsoft.AspNetCore.Components.EditorRequiredAttribute";
    private const string ComponentInterfaceName = "Microsoft.AspNetCore.Components.IComponent";
    private const string RenderFragmentName = "Microsoft.AspNetCore.Components.RenderFragment";
    private const string RenderFragmentOfTName = "Microsoft.AspNetCore.Components.RenderFragment<TValue>";
    private const string EventCallbackName = "Microsoft.AspNetCore.Components.EventCallback";
    private const string EventCallbackOfTName = "Microsoft.AspNetCore.Components.EventCallback<TValue>";

    private const string Ns = "global::Grove.Display.Management.Components.";

    private static readonly DiagnosticDescriptor s_markupMarker = new(
        MarkupMarkerDiagnosticId,
        "Component marked in Razor markup",
        "'{0}' marks a component with @attribute [BlazorComponent]; the component catalog only reads C# declarations, so it is left out. Move the attribute to the code-behind (.razor.cs).",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor s_markupParameter = new(
        MarkupParameterDiagnosticId,
        "Component parameters declared in Razor markup",
        "'{0}' declares [Parameter] properties in an @code block; the component catalog only reads C# declarations, so they are missing from the '{1}' schema. Move them to the code-behind (.razor.cs).",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor s_skipped = new(
        SkippedComponentDiagnosticId,
        "Component left out of the catalog",
        "'{0}' is marked [BlazorComponent] but is left out of the component catalog: {1}",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var components = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                MarkerAttributeName,
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (context, cancellationToken) => Describe(context, cancellationToken))
            .Where(static component => component is not null)
            .Select(static (component, _) => component!);

        var assembly = context.CompilationProvider.Select(static (compilation, _) => new AssemblyModel(
            compilation.AssemblyName ?? "Assembly",
            ShellsOf(compilation),
            compilation.GetTypeByMetadataName(CatalogInterfaceName) is not null));

        var razorFiles = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
            .Select(static (file, cancellationToken) => ScanRazor(file, cancellationToken))
            .Where(static file => file is not null)
            .Select(static (file, _) => file!);

        context.RegisterSourceOutput(
            components.Collect().Combine(assembly),
            static (output, input) => Emit(output, input.Left, input.Right));

        context.RegisterSourceOutput(
            razorFiles.Combine(components.Collect()),
            static (output, input) => ReportRazor(output, input.Left, input.Right));
    }

    private static ComponentModel? Describe(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        if (context.TargetSymbol is not INamedTypeSymbol type || context.Attributes.Length == 0)
        {
            return null;
        }

        var compilation = context.SemanticModel.Compilation;
        var marker = context.Attributes[0];
        var location = context.TargetNode.GetLocation();
        var displayName = type.ToDisplayString();

        if (marker.ConstructorArguments.Length != 1 || marker.ConstructorArguments[0].Value is not string name || name.Length == 0)
        {
            return ComponentModel.Skipped(displayName, "it has no component name", location);
        }

        if (type.IsAbstract)
        {
            return ComponentModel.Skipped(displayName, "an abstract class cannot be rendered", location);
        }

        if (type.IsGenericType)
        {
            return ComponentModel.Skipped(displayName, "a generic component cannot be rendered by name", location);
        }

        var componentInterface = compilation.GetTypeByMetadataName(ComponentInterfaceName);
        if (componentInterface is null || !type.AllInterfaces.Contains(componentInterface, SymbolEqualityComparer.Default))
        {
            return ComponentModel.Skipped(displayName, "it is not a Razor component (IComponent)", location);
        }

        if (!compilation.IsSymbolAccessibleWithin(type, compilation.Assembly))
        {
            return ComponentModel.Skipped(displayName, "it is not accessible to the assembly's generated catalog", location);
        }

        var code = new StringBuilder();
        code.Append("new ").Append(Ns).Append("ComponentDescriptor(\n");
        code.Append("                ").Append(Literal(name)).Append(",\n");
        code.Append("                typeof(").Append(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Append("),\n");

        var parameters = new List<string>();
        var zones = new List<string>();
        foreach (var property in ParameterProperties(type))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsZone(property.Type))
            {
                zones.Add(ZoneCode(property));
            }
            else
            {
                parameters.Add(ParameterCode(property, compilation));
            }
        }

        code.Append("                new ").Append(Ns).Append("ParameterDescriptor[] {");
        AppendList(code, parameters);
        code.Append("},\n");
        code.Append("                new ").Append(Ns).Append("ZoneDescriptor[] {");
        AppendList(code, zones);
        code.Append("},\n");
        code.Append("                Shells,\n");
        code.Append("                Module: ModuleName");

        string? Named(string argument) => NamedArgument(marker, argument)?.Value as string;
        AppendNamed(code, "Permission", Named("Permission"));
        if (NamedArgument(marker, "Instances")?.Value is int instances && instances != 0)
        {
            code.Append(",\n                Instances: (").Append(Ns).Append("ComponentInstances)").Append(instances.ToString(CultureInfo.InvariantCulture));
        }

        if (NamedArgument(marker, "IsWrapper")?.Value is true)
        {
            code.Append(",\n                IsWrapper: true");
        }

        if (NamedArgument(marker, "IsHost")?.Value is true)
        {
            code.Append(",\n                IsHost: true");
        }

        AppendNamed(code, "DisplayName", Named("DisplayName"));
        AppendNamed(code, "Category", Named("Category"));
        AppendNamed(code, "Icon", Named("Icon"));
        if (NamedArgument(marker, "IsOverlay")?.Value is true)
        {
            code.Append(",\n                IsOverlay: true");
        }

        code.Append(')');
        if (NamedArgument(marker, "Edits") is { Kind: TypedConstantKind.Array, IsNull: false } edits)
        {
            code.Append(" { Edits = new string[] { ")
                .Append(string.Join(", ", edits.Values.Select(value => Literal(value.Value as string ?? string.Empty))))
                .Append(" } }");
        }

        return new ComponentModel(name, type.Name, code.ToString(), null, null);
    }

    // Base class parameters first, each type's in declaration order; a redeclared name keeps its
    // first position and takes the most derived declaration (as ReflectionComponentCatalog does).
    private static IEnumerable<IPropertySymbol> ParameterProperties(INamedTypeSymbol type)
    {
        var hierarchy = new Stack<INamedTypeSymbol>();
        for (var current = type; current is not null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
        {
            hierarchy.Push(current);
        }

        var byName = new Dictionary<string, IPropertySymbol>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var level in hierarchy)
        {
            foreach (var property in level.GetMembers().OfType<IPropertySymbol>())
            {
                if (property.IsStatic || property.IsIndexer || property.DeclaredAccessibility != Accessibility.Public)
                {
                    continue;
                }

                var parameter = property.GetAttributes().FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString() == ParameterAttributeName);
                if (parameter is null || NamedArgument(parameter, "CaptureUnmatchedValues")?.Value is true)
                {
                    continue;
                }

                if (!byName.ContainsKey(property.Name))
                {
                    order.Add(property.Name);
                }

                byName[property.Name] = property;
            }
        }

        return order.Select(name => byName[name]);
    }

    private static string ParameterCode(IPropertySymbol property, Compilation compilation)
    {
        var type = property.Type;
        var accessible = type.TypeKind != TypeKind.Error && !ContainsTypeParameter(type) && compilation.IsSymbolAccessibleWithin(type, compilation.Assembly);
        var (kind, editor, choices) = accessible ? Infer(type, compilation) : ("Object", "object", null);
        var typeName = accessible ? TypeOf(type) : "object";

        var refinement = Attribute(property, ParameterRefinementName);
        if (refinement is not null && NamedArgument(refinement, "Choices") is { Kind: TypedConstantKind.Array, IsNull: false } declared)
        {
            (kind, editor, choices) = ("Choice", "enum", declared.Values.Select(value => value.Value as string ?? string.Empty).ToArray());
        }

        // An EventCallback<T> marked [ComponentOutput] publishes its value (§ 4a).
        var output = Attribute(property, OutputRefinementName);
        if (output is not null && kind == "EventHandler")
        {
            (kind, editor) = ("Output", "output");
        }

        var required = (refinement is not null && NamedArgument(refinement, "Required")?.Value is true)
            || Attribute(property, EditorRequiredAttributeName) is not null;

        var code = new StringBuilder();
        code.Append("new ").Append(Ns).Append("ParameterDescriptor(")
            .Append(Literal(property.Name))
            .Append(", typeof(").Append(typeName).Append(')')
            .Append(", ").Append(Ns).Append("ParameterKind.").Append(kind)
            .Append(", ").Append(Literal(refinement is not null && NamedArgument(refinement, "Editor")?.Value is string custom ? custom : editor));

        var parameterDisplayName = (refinement is not null ? NamedArgument(refinement, "DisplayName")?.Value as string : null)
            ?? (output is not null ? NamedArgument(output, "DisplayName")?.Value as string : null);
        AppendNamed(code, "DisplayName", parameterDisplayName, inline: true);
        if (refinement is not null)
        {
            AppendNamed(code, "Group", NamedArgument(refinement, "Group")?.Value as string, inline: true);
        }

        if (required)
        {
            code.Append(", Required: true");
        }

        foreach (var flag in new[] { "Hidden", "Advanced", "ReadOnly" })
        {
            if (refinement is not null && NamedArgument(refinement, flag)?.Value is true)
            {
                code.Append(", ").Append(flag).Append(": true");
            }
        }

        if (choices is not null)
        {
            code.Append(", Choices: new string[] { ").Append(string.Join(", ", choices.Select(Literal))).Append(" }");
        }

        if (refinement is not null && NamedArgument(refinement, "Accepts") is { Kind: TypedConstantKind.Array, IsNull: false } accepts)
        {
            code.Append(", Accepts: new string[] { ")
                .Append(string.Join(", ", accepts.Values.Select(value => Literal(value.Value as string ?? string.Empty))))
                .Append(" }");
        }

        // The payload or output of an EventCallback<T>.
        if (accessible && type is INamedTypeSymbol { IsGenericType: true } callback
            && callback.OriginalDefinition.ToDisplayString() == EventCallbackOfTName
            && compilation.IsSymbolAccessibleWithin(callback.TypeArguments[0], compilation.Assembly))
        {
            code.Append(", ValueType: typeof(").Append(TypeOf(callback.TypeArguments[0])).Append(')');
        }

        return code.Append(')').ToString();
    }

    private static string ZoneCode(IPropertySymbol property)
    {
        var refinement = Attribute(property, ZoneRefinementName);
        var allowed = refinement is not null && NamedArgument(refinement, "Allowed") is { Kind: TypedConstantKind.Array, IsNull: false } values
            ? values.Values.Select(value => Literal(value.Value as string ?? string.Empty)).ToArray()
            : [];
        var min = refinement is not null && NamedArgument(refinement, "Min")?.Value is int minimum ? minimum : 0;
        var max = refinement is not null && NamedArgument(refinement, "Max")?.Value is int maximum ? maximum : 0;

        return $"new {Ns}ZoneDescriptor({Literal(property.Name)}, new string[] {{ {string.Join(", ", allowed)} }}, {min.ToString(CultureInfo.InvariantCulture)}, {max.ToString(CultureInfo.InvariantCulture)})";
    }

    // Mirrors ComponentSchema.Infer (Grove.Display.Management.Abstractions).
    private static (string Kind, string Editor, string[]? Choices) Infer(ITypeSymbol type, Compilation compilation)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
        {
            type = nullable.TypeArguments[0];
        }

        switch (type.SpecialType)
        {
            case SpecialType.System_String:
            case SpecialType.System_Char:
                return ("String", "text", null);
            case SpecialType.System_Boolean:
                return ("Boolean", "bool", null);
            case SpecialType.System_DateTime:
                return ("String", "date", null);
            case SpecialType.System_Byte:
            case SpecialType.System_SByte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
            case SpecialType.System_Single:
            case SpecialType.System_Double:
            case SpecialType.System_Decimal:
                return ("Number", "number", null);
        }

        if (type.TypeKind == TypeKind.Enum)
        {
            return ("Choice", "enum", EnumNames((INamedTypeSymbol)type));
        }

        var name = type.OriginalDefinition.ToDisplayString();
        switch (name)
        {
            case "System.Guid":
            case "System.Uri":
                return ("String", "text", null);
            case "System.DateTimeOffset":
            case "System.DateOnly":
                return ("String", "date", null);
            case "System.TimeOnly":
            case "System.TimeSpan":
                return ("String", "time", null);
            case EventCallbackName:
            case EventCallbackOfTName:
                return ("EventHandler", "action", null);
        }

        if (type.TypeKind == TypeKind.Array || (IsEnumerable(type, compilation) && !IsDictionary(type)))
        {
            return ("Array", "list", null);
        }

        return ("Object", "object", null);
    }

    // Enum.GetNames order: by value, compared as unsigned.
    private static string[] EnumNames(INamedTypeSymbol type) => type.GetMembers()
        .OfType<IFieldSymbol>()
        .Where(field => field.HasConstantValue)
        .OrderBy(field => unchecked((ulong)Convert.ToInt64(field.ConstantValue, CultureInfo.InvariantCulture)))
        .Select(field => field.Name)
        .ToArray();

    private static bool IsZone(ITypeSymbol type) =>
        type.OriginalDefinition.ToDisplayString() is RenderFragmentName or RenderFragmentOfTName;

    private static bool IsEnumerable(ITypeSymbol type, Compilation compilation)
    {
        var enumerable = compilation.GetSpecialType(SpecialType.System_Collections_IEnumerable);
        return SymbolEqualityComparer.Default.Equals(type, enumerable)
            || type.AllInterfaces.Any(candidate => SymbolEqualityComparer.Default.Equals(candidate, enumerable));
    }

    private static bool IsDictionary(ITypeSymbol type)
    {
        static bool IsDictionaryInterface(ITypeSymbol candidate) => candidate.OriginalDefinition.ToDisplayString() is
            "System.Collections.IDictionary"
            or "System.Collections.Generic.IDictionary<TKey, TValue>"
            or "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>";

        return IsDictionaryInterface(type) || type.AllInterfaces.Any(IsDictionaryInterface);
    }

    private static bool ContainsTypeParameter(ITypeSymbol type) => type switch
    {
        ITypeParameterSymbol => true,
        IArrayTypeSymbol array => ContainsTypeParameter(array.ElementType),
        INamedTypeSymbol named => named.TypeArguments.Any(ContainsTypeParameter),
        _ => false,
    };

    private static string TypeOf(ITypeSymbol type) =>
        type.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    private static string ShellsOf(Compilation compilation)
    {
        var declared = compilation.Assembly.GetAttributes()
            .FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString() == ShellAttributeName);

        var shell = declared is { ConstructorArguments.Length: 1 } ? declared.ConstructorArguments[0].Value as string : null;
        return shell switch
        {
            "admin" => "Admin",
            "site" => "Site",
            "member" => "Member",
            _ => "Admin,Site,Member",
        };
    }

    private static void Emit(SourceProductionContext output, ImmutableArray<ComponentModel> components, AssemblyModel assembly)
    {
        foreach (var skipped in components.Where(component => component.SkipReason is not null))
        {
            output.ReportDiagnostic(Diagnostic.Create(s_skipped, skipped.Location, skipped.TypeName, skipped.SkipReason));
        }

        var described = components.Where(component => component.Code is not null).ToArray();
        if (described.Length == 0 || !assembly.HasCatalogContract)
        {
            return;
        }

        var className = "ComponentCatalog_" + Identifier(assembly.Name);
        var source = new StringBuilder();
        source.Append("// <auto-generated/>\n");
        source.Append("#nullable enable\n\n");
        source.Append("[assembly: ").Append(Ns).Append("ComponentCatalogAttribute(typeof(").Append(Ns).Append("Generated.").Append(className).Append("))]\n\n");
        source.Append("namespace Grove.Display.Management.Components.Generated\n{\n");
        source.Append("    /// <summary>The components of ").Append(assembly.Name).Append(", generated from their declarations.</summary>\n");
        source.Append("    [global::System.CodeDom.Compiler.GeneratedCode(\"Grove.SourceGenerators\", \"1.0\")]\n");
        source.Append("    internal sealed class ").Append(className).Append(" : ").Append(Ns).Append("IComponentCatalog\n    {\n");
        source.Append("        private const string ModuleName = ").Append(Literal(assembly.Name)).Append(";\n\n");
        source.Append("        private static readonly global::System.Collections.Generic.IReadOnlySet<").Append(Ns).Append("ShellKind> Shells =\n");
        source.Append("            new global::System.Collections.Generic.HashSet<").Append(Ns).Append("ShellKind> { ")
            .Append(string.Join(", ", assembly.Shells.Split(',').Select(shell => Ns + "ShellKind." + shell))).Append(" };\n\n");
        source.Append("        private static ").Append(Ns).Append("ComponentDescriptor[]? s_components;\n\n");
        source.Append("        public global::System.Collections.Generic.IEnumerable<").Append(Ns).Append("ComponentDescriptor> Describe() => s_components ??= Build();\n\n");
        source.Append("        private static ").Append(Ns).Append("ComponentDescriptor[] Build() => new ").Append(Ns).Append("ComponentDescriptor[]\n        {\n");
        foreach (var component in described.OrderBy(component => component.Name, StringComparer.Ordinal))
        {
            source.Append("            ").Append(component.Code).Append(",\n");
        }

        source.Append("        };\n    }\n}\n");

        output.AddSource("ComponentCatalog.g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
    }

    private static RazorFile? ScanRazor(AdditionalText file, CancellationToken cancellationToken)
    {
        var text = file.GetText(cancellationToken)?.ToString();
        if (text is null)
        {
            return null;
        }

        const string Usage = "BlazorComponent(";
        var marker = text.IndexOf(Usage, StringComparison.Ordinal);
        var markedInMarkup = marker >= 0 && text.LastIndexOf("@attribute", marker, StringComparison.Ordinal) is var directive and >= 0
            && text.IndexOf('\n', directive) is var lineEnd && (lineEnd < 0 || lineEnd > marker);
        var hasParameters = text.IndexOf("[Parameter", StringComparison.Ordinal) >= 0;

        if (!markedInMarkup && !hasParameters)
        {
            return null;
        }

        var fileName = Path.GetFileNameWithoutExtension(file.Path);
        var span = markedInMarkup ? new TextSpan(marker, Usage.Length - 1) : new TextSpan(0, 0);
        var lines = file.GetText(cancellationToken)!.Lines.GetLinePositionSpan(span);
        return new RazorFile(file.Path, fileName, markedInMarkup, hasParameters, span, lines);
    }

    private static void ReportRazor(SourceProductionContext output, RazorFile file, ImmutableArray<ComponentModel> components)
    {
        var location = Location.Create(file.Path, file.Span, file.Lines);
        if (file.MarkedInMarkup)
        {
            output.ReportDiagnostic(Diagnostic.Create(s_markupMarker, location, file.FileName + ".razor"));
        }

        var component = components.FirstOrDefault(candidate => candidate.Code is not null && candidate.TypeName == file.FileName);
        if (file.HasParameters && component is not null)
        {
            output.ReportDiagnostic(Diagnostic.Create(s_markupParameter, location, file.FileName + ".razor", component.Name));
        }
    }

    private static AttributeData? Attribute(ISymbol symbol, string name) =>
        symbol.GetAttributes().FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString() == name);

    private static TypedConstant? NamedArgument(AttributeData attribute, string name)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.Key == name)
            {
                return argument.Value;
            }
        }

        return null;
    }

    private static void AppendNamed(StringBuilder code, string name, string? value, bool inline = false)
    {
        if (value is not null)
        {
            code.Append(inline ? ", " : ",\n                ").Append(name).Append(": ").Append(Literal(value));
        }
    }

    private static void AppendList(StringBuilder code, List<string> items)
    {
        if (items.Count == 0)
        {
            return;
        }

        code.Append('\n');
        foreach (var item in items)
        {
            code.Append("                    ").Append(item).Append(",\n");
        }

        code.Append("                ");
    }

    private static string Literal(string value) => SymbolDisplay.FormatLiteral(value, quote: true);

    private static string Identifier(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (var character in name)
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : '_');
        }

        return builder.ToString();
    }

    // Shells as a comma-separated list, so the model stays equatable for incremental caching.
    private sealed record AssemblyModel(string Name, string Shells, bool HasCatalogContract);

    private sealed record ComponentModel(string Name, string TypeName, string? Code, string? SkipReason, Location? Location)
    {
        public static ComponentModel Skipped(string typeName, string reason, Location location) =>
            new(typeName, typeName, null, reason, location);
    }

    private sealed record RazorFile(string Path, string FileName, bool MarkedInMarkup, bool HasParameters, TextSpan Span, LinePositionSpan Lines);
}
