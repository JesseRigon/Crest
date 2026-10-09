using Microsoft.Extensions.DependencyInjection;
using Crest.Modules;

namespace Crest.Media.Indexing.Pdf;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddMediaFileTextProvider<PdfMediaFileTextProvider>(".pdf");
    }
}
