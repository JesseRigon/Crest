using System.Reflection;

namespace Crest.Workflows.UIHints.RadioList;

/// <inheritdoc />
public class RadioListUIHintHandler : IUIHintHandler
{
    /// <inheritdoc />
    public string UIHint => InputUIHints.RadioList;

    /// <inheritdoc />
    public ValueTask<IEnumerable<Type>> GetPropertyUIHandlersAsync(PropertyInfo propertyInfo, CancellationToken cancellationToken)
    {
        return new([typeof(StaticRadioListOptionsProvider)]);
    }
}