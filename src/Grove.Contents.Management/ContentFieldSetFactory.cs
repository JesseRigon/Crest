using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Grove.Contents.Management;

/// <summary>
/// Implements <see cref="ITypeActivatorFactory{ContentFieldSet}"/> by resolving all registered <see cref="ContentFieldSet"/> types
/// and memorizing a statically typed <see cref="ITypeActivator{ContentFieldSet}"/>.
/// </summary>
public class ContentFieldSetFactory : ITypeActivatorFactory<ContentFieldSet>
{
    private readonly ITypeActivator<ContentFieldSet> _contentFieldSetActivator = new GenericTypeActivator<ContentFieldSet, ContentFieldSet>();

    private readonly Dictionary<string, ITypeActivator<ContentFieldSet>> _contentFieldSetActivators;

    public ContentFieldSetFactory(IOptions<ContentOptions> contentOptions, ILogger<ContentFieldSetFactory> logger)
    {
        _contentFieldSetActivators = [];

        // Check content options for configured parts.
        foreach (var partOption in contentOptions.Value.ContentFieldSetOptions)
        {
            if (_contentFieldSetActivators.ContainsKey(ContentFieldSetName.Of(partOption.Type)))
            {
                logger.LogWarning("The ContentFieldSet '{Name}' was registered more than once. Field sets should only be registered once using .AddContentFieldSet<{Name}>().", ContentFieldSetName.Of(partOption.Type), ContentFieldSetName.Of(partOption.Type));

                continue;
            }

            var activatorType = typeof(GenericTypeActivator<,>).MakeGenericType(partOption.Type, typeof(ContentFieldSet));
            var activator = (ITypeActivator<ContentFieldSet>)Activator.CreateInstance(activatorType);

            _contentFieldSetActivators.Add(ContentFieldSetName.Of(partOption.Type), activator);
        }
    }

    /// <inheritdoc />
    public ITypeActivator<ContentFieldSet> GetTypeActivator(string fieldSetName)
    {
        if (_contentFieldSetActivators.TryGetValue(fieldSetName, out var activator))
        {
            return activator;
        }

        return _contentFieldSetActivator;
    }
}
