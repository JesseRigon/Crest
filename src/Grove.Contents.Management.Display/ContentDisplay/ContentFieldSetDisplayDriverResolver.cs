using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Grove.Contents.Management.Display.ContentDisplay;

public class ContentFieldSetDisplayDriverResolver : IContentFieldSetDisplayDriverResolver
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ContentDisplayOptions _contentDisplayOptions;
    public ContentFieldSetDisplayDriverResolver(
        IServiceProvider serviceProvider,
        IOptions<ContentDisplayOptions> contentDisplayOptions
        )
    {
        _serviceProvider = serviceProvider;
        _contentDisplayOptions = contentDisplayOptions.Value;
    }

    public IList<IContentFieldSetDisplayDriver> GetDisplayModeDrivers(string fieldSetName, string displayMode)
    {
        var services = new List<IContentFieldSetDisplayDriver>();

        if (_contentDisplayOptions.ContentFieldSetOptions.TryGetValue(fieldSetName, out var contentFieldSetDisplayOption))
        {
            foreach (var displayDriverOption in contentFieldSetDisplayOption.DisplayDrivers)
            {
                if (displayDriverOption.DisplayMode.Invoke(displayMode))
                {
                    services.Add((IContentFieldSetDisplayDriver)_serviceProvider.GetRequiredService(displayDriverOption.DisplayDriverType));
                }
            }
        }

        return services;
    }

    public IList<IContentFieldSetDisplayDriver> GetEditorDrivers(string fieldSetName, string editor)
    {
        var services = new List<IContentFieldSetDisplayDriver>();

        if (_contentDisplayOptions.ContentFieldSetOptions.TryGetValue(fieldSetName, out var contentFieldSetDisplayOption))
        {
            foreach (var editorDriverOption in contentFieldSetDisplayOption.EditorDrivers)
            {
                if (editorDriverOption.Editor.Invoke(editor))
                {
                    services.Add((IContentFieldSetDisplayDriver)_serviceProvider.GetRequiredService(editorDriverOption.DisplayDriverType));
                }
            }
        }

        return services;
    }
}
