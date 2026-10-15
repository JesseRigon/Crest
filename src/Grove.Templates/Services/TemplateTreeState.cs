using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Grove.DisplayManagement.Components;
using Grove.Infrastructure.Html;
using Fluid;
using Fluid.Values;

namespace Grove.Templates.Services;

/// <summary>Raised when an evaluation overruns a limit; the evaluator turns it into an error node.</summary>
public sealed class TemplateLimitException(string message) : Exception(message);

/// <summary>
/// The budget one page's template evaluations share (ruling 2026-10-12, render limits): the
/// deadline, the node count and the composite nesting, so a composite cannot reset the limits
/// its parent runs under.
/// </summary>
public sealed class TemplateBudget(DisplayTreeOptions options, CancellationToken cancellationToken)
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private int _nodes;

    public DisplayTreeOptions Options { get; } = options;

    public CancellationToken CancellationToken { get; } = cancellationToken;

    /// <summary>The composites being evaluated, outermost first; a name twice is a cycle.</summary>
    public List<string> Stack { get; } = [];

    public TimeSpan Remaining => Options.Timeout - _clock.Elapsed;

    public void Count()
    {
        if (++_nodes > Options.MaxNodes)
        {
            throw new TemplateLimitException($"The templates emitted more than {Options.MaxNodes} nodes.");
        }

        Check();
    }

    public void Check()
    {
        CancellationToken.ThrowIfCancellationRequested();
        if (_clock.Elapsed > Options.Timeout)
        {
            throw new TemplateLimitException($"Evaluating the templates took longer than {Options.Timeout.TotalSeconds:0.###} s.");
        }
    }
}

/// <summary>
/// What one template evaluation builds (docs/blazor-display.md § 5): the frames of the open
/// <c>compose</c>, <c>zone</c> and <c>on</c> blocks, the text written since the last tag (a
/// sanitized markup leaf when the next tag comes), the diagnostics. Kept in the template
/// context's ambient values; the tags read it from there.
/// </summary>
public sealed class TemplateTreeState
{
    public const string AmbientKey = "Grove.Templates.TreeState";

    private readonly StringBuilder _text = new();
    private readonly Stack<Frame> _frames = new();
    private readonly IHtmlSanitizerService? _sanitizer;
    private int _counter;

    public TemplateTreeState(string prefix, IComponentRegistry registry, IHtmlSanitizerService? sanitizer, TemplateBudget budget, IServiceProvider services, TemplateTreeEvaluator evaluator)
    {
        Prefix = prefix;
        Registry = registry;
        _sanitizer = sanitizer;
        Budget = budget;
        Services = services;
        Evaluator = evaluator;
        _frames.Push(new Frame(null, null));
    }

    /// <summary>The instance id the template's nodes are scoped under: explicit ids become <c>{prefix}.{id}</c>.</summary>
    public string Prefix { get; }

    public IComponentRegistry Registry { get; }

    public TemplateBudget Budget { get; }

    public IServiceProvider Services { get; }

    public TemplateTreeEvaluator Evaluator { get; }

    public List<DisplayDiagnostic> Diagnostics { get; } = [];

    public List<DisplayNode> Roots { get; } = [];

    public static TemplateTreeState From(TemplateContext context) =>
        context.AmbientValues.TryGetValue(AmbientKey, out var state) && state is TemplateTreeState tree
            ? tree
            : throw new InvalidOperationException("This tag emits display-tree nodes and runs only in a template.");

    /// <summary>An explicit id scoped to this evaluation, so a template placed twice on a page keeps its ids apart.</summary>
    public string Scope(string id) => Prefix + "." + id;

    /// <summary>A generated id: deterministic from the prefix and the order nodes are emitted in.</summary>
    public string NextId() =>
        "n" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Prefix + "#" + _counter++)))[..16];

    public void Write(string? text) => _text.Append(text);

    public void Write(char text) => _text.Append(text);

    public void Diagnose(string path, string action, string reason) => Diagnostics.Add(new(path, action, reason));

    public Frame Current => _frames.Peek();

    public void Push(Frame frame)
    {
        FlushText();
        _frames.Push(frame);
    }

    public void Pop()
    {
        FlushText();
        if (_frames.Count > 1)
        {
            _frames.Pop();
        }
    }

    /// <summary>Text between tags becomes a sanitized markup leaf; whitespace alone is dropped.</summary>
    public void FlushText()
    {
        if (_text.Length == 0)
        {
            return;
        }

        var text = _text.ToString();
        _text.Clear();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (Current.Actions is not null)
        {
            Diagnose(Current.Node?.Id ?? Prefix, "dropped", "Text inside an 'on' block is ignored; only actions belong there.");
            return;
        }

        var html = _sanitizer?.Sanitize(text) ?? string.Empty;
        Append(DisplayNode.Markup(NextId(), html), checkZone: false);
    }

    /// <summary>Adds a node to the open block: the root list, or the current zone of the open component.</summary>
    public void Append(DisplayNode node, bool checkZone = true)
    {
        if (checkZone)
        {
            FlushText();
        }

        Budget.Count();
        var frame = Current;
        if (frame.Actions is not null)
        {
            Diagnose(node.Id, "dropped", $"'{node.Component}' cannot be placed inside an 'on' block.");
            return;
        }

        if (frame.Node is null)
        {
            Roots.Add(node);
            return;
        }

        var descriptor = frame.Descriptor!;
        var zoneName = frame.Zone ?? (descriptor.Zones.Count > 0 ? descriptor.Zones[0].Name : null);
        if (zoneName is null || descriptor.Zone(zoneName) is not { } zone)
        {
            Diagnose(node.Id, "dropped", $"'{descriptor.Name}' has no zone '{zoneName ?? "(none)"}' for '{node.Component}'.");
            return;
        }

        if (checkZone && node.Component is not (DisplayNode.MarkupComponent or DisplayNode.ShapeZoneComponent or DisplayNode.ErrorComponent) && !zone.Admits(node.Component))
        {
            Diagnose(node.Id, "dropped", $"The zone '{zone.Name}' of '{descriptor.Name}' does not admit '{node.Component}'.");
            return;
        }

        if (!frame.Node.Zones.TryGetValue(zone.Name, out var children))
        {
            frame.Node.Zones[zone.Name] = children = [];
        }

        if (zone.Max > 0 && children.Count >= zone.Max)
        {
            Diagnose(node.Id, "dropped", $"The zone '{zone.Name}' of '{descriptor.Name}' holds at most {zone.Max} item(s).");
            return;
        }

        children.Add(node);
    }

    /// <summary>A tag argument as a binding: a <see cref="BindingValue"/> as it is, anything else as a literal.</summary>
    public static DisplayBinding ToBinding(FluidValue value) =>
        value is BindingValue binding ? binding.Binding : DisplayBinding.Literal(ToJson(value));

    public static JsonNode? ToJson(FluidValue value) => value switch
    {
        NilValue or EmptyValue => null,
        BooleanValue boolean => JsonValue.Create(boolean.ToBooleanValue()),
        NumberValue number => JsonValue.Create(number.ToNumberValue()),
        StringValue text => JsonValue.Create(text.ToStringValue()),
        _ => DisplayState.ToJson(value.ToObjectValue()),
    };

    /// <summary>One open block.</summary>
    public sealed class Frame(DisplayNode? node, ComponentDescriptor? descriptor)
    {
        public DisplayNode? Node { get; } = node;

        public ComponentDescriptor? Descriptor { get; } = descriptor;

        /// <summary>The zone children go to; the component's first zone when null.</summary>
        public string? Zone { get; set; }

        /// <summary>Set inside an <c>on</c> block: the actions it collects.</summary>
        public List<DisplayAction>? Actions { get; init; }
    }
}

/// <summary>The writer statements write to: everything goes to the state's pending text.</summary>
internal sealed class TemplateTreeWriter(TemplateTreeState state) : TextWriter
{
    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char value) => state.Write(value);

    public override void Write(string? value) => state.Write(value);

    public override void Write(char[] buffer, int index, int count) => state.Write(new string(buffer, index, count));
}
