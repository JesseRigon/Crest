using Microsoft.Extensions.DependencyInjection;
using Crest.Modules;

namespace Crest.Media.Indexing.OpenXML;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddMediaFileTextProvider<WordDocumentMediaFileTextProvider>(".docx");
        services.AddMediaFileTextProvider<PresentationDocumentMediaFileTextProvider>(".pptx");
    }
}
