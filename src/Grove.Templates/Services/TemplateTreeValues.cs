using System.Globalization;
using System.Text.Encodings.Web;
using Grove.DisplayManagement.Components;
using Fluid;
using Fluid.Values;

namespace Grove.Templates.Services;

/// <summary>
/// A run-time binding written in a template (docs/blazor-display.md § 4a): <c>nodes.invoices.selected.id</c>,
/// <c>page.row</c>, <c>event.item.id</c>. It has no value while the template is evaluated (the
/// value is the viewer's, in the browser); a tag argument that receives one records the binding
/// on the node instead of a literal. Member and index access extend its path.
/// </summary>
public sealed class BindingValue(DisplayBinding binding) : FluidValue
{
    public DisplayBinding Binding { get; } = binding;

    public override FluidValues Type => FluidValues.Object;

    public override bool Equals(FluidValue other) => other is BindingValue value && ReferenceEquals(value.Binding, Binding);

    public override bool ToBooleanValue() => false;

    public override decimal ToNumberValue() => 0;

    public override string ToStringValue() => string.Empty;

    public override object ToObjectValue() => Binding;

    [Obsolete("WriteTo is obsolete, prefer the WriteToAsync method.")]
    public override void WriteTo(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
    {
    }

    public override ValueTask<FluidValue> GetValueAsync(string name, TemplateContext context) => new(Extend(Append(Binding.Path, name)));

    public override ValueTask<FluidValue> GetIndexAsync(FluidValue index, TemplateContext context) =>
        new(Extend((Binding.Path ?? string.Empty) + "[" + index.ToNumberValue().ToString("0", CultureInfo.InvariantCulture) + "]"));

    public override int GetHashCode() => Binding.GetHashCode();

    private BindingValue Extend(string path) => new(new DisplayBinding
    {
        Node = Binding.Node,
        Output = Binding.Output,
        Variable = Binding.Variable,
        Event = Binding.Event,
        Value = Binding.Value,
        Path = path,
    });

    private static string Append(string? path, string member) => string.IsNullOrEmpty(path) ? member : path + "." + member;
}

/// <summary><c>nodes</c>: a node of this template by its id; its members are the node's outputs.</summary>
public sealed class NodesValue(Func<string, string> scope) : FluidValue
{
    public override FluidValues Type => FluidValues.Object;

    public override bool Equals(FluidValue other) => ReferenceEquals(this, other);

    public override bool ToBooleanValue() => true;

    public override decimal ToNumberValue() => 0;

    public override string ToStringValue() => string.Empty;

    public override object ToObjectValue() => this;

    [Obsolete("WriteTo is obsolete, prefer the WriteToAsync method.")]
    public override void WriteTo(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
    {
    }

    public override ValueTask<FluidValue> GetValueAsync(string name, TemplateContext context) => new(new NodeValue(scope(name)));

    public override int GetHashCode() => 0;

    private sealed class NodeValue(string nodeId) : FluidValue
    {
        public override FluidValues Type => FluidValues.Object;

        public override bool Equals(FluidValue other) => other is NodeValue node && node.ToStringValue() == nodeId;

        public override bool ToBooleanValue() => true;

        public override decimal ToNumberValue() => 0;

        public override string ToStringValue() => nodeId;

        public override object ToObjectValue() => nodeId;

        [Obsolete("WriteTo is obsolete, prefer the WriteToAsync method.")]
        public override void WriteTo(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
        {
        }

        public override ValueTask<FluidValue> GetValueAsync(string name, TemplateContext context) =>
            new(new BindingValue(DisplayBinding.FromOutput(nodeId, ToOutputName(name))));

        public override int GetHashCode() => nodeId.GetHashCode(StringComparison.Ordinal);

        // Liquid members are snake or camel case; outputs are parameter names ("selected" → "Selected",
        // "selected_item" → "SelectedItem"). The validator checks the name against the schema.
        private static string ToOutputName(string name) =>
            string.Concat(name.Split('_', StringSplitOptions.RemoveEmptyEntries).Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }
}

/// <summary><c>page</c>: the page variables set by actions.</summary>
public sealed class PageVariablesValue : FluidValue
{
    public override FluidValues Type => FluidValues.Object;

    public override bool Equals(FluidValue other) => ReferenceEquals(this, other);

    public override bool ToBooleanValue() => true;

    public override decimal ToNumberValue() => 0;

    public override string ToStringValue() => string.Empty;

    public override object ToObjectValue() => this;

    [Obsolete("WriteTo is obsolete, prefer the WriteToAsync method.")]
    public override void WriteTo(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
    {
    }

    public override ValueTask<FluidValue> GetValueAsync(string name, TemplateContext context) => new(new BindingValue(DisplayBinding.FromVariable(name)));

    public override int GetHashCode() => 1;
}
