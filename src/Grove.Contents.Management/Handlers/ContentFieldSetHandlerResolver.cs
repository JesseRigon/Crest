using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Grove.Contents.Management.Handlers;

public class ContentFieldSetHandlerResolver : IContentFieldSetHandlerResolver
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ContentOptions _contentOptions;

    public ContentFieldSetHandlerResolver(
        IServiceProvider serviceProvider,
        IOptions<ContentOptions> contentDisplayOptions
        )
    {
        _serviceProvider = serviceProvider;
        _contentOptions = contentDisplayOptions.Value;
    }

    public IList<IContentFieldSetHandler> GetHandlers(string fieldSetName)
    {
        var services = new List<IContentFieldSetHandler>();

        if (_contentOptions.ContentFieldSetOptionsLookup.TryGetValue(fieldSetName, out var contentFieldSetOption))
        {
            foreach (var handlerOption in contentFieldSetOption.Handlers)
            {
                services.Add((IContentFieldSetHandler)_serviceProvider.GetRequiredService(handlerOption));
            }
        }

        return services;
    }
}
