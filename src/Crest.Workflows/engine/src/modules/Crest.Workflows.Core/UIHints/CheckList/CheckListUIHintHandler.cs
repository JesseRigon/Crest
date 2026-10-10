using System.Reflection;

namespace Crest.Workflows.UIHints.CheckList;

/// <inheritdoc />
public class CheckListUIHintHandler : IUIHintHandler
{
    /// <inheritdoc />
    public string UIHint => InputUIHints.CheckList;

    /// <inheritdoc />
    public ValueTask<IEnumerable<Type>> GetPropertyUIHandlersAsync(PropertyInfo propertyInfo, CancellationToken cancellationToken)
    {
        return new([typeof(StaticCheckListOptionsProvider)]);
    }
}