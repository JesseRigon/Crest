using Microsoft.Extensions.DependencyInjection;
using Grove.Modules;

namespace Grove.Media.Indexing.Pdf;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddMediaFileTextProvider<PdfMediaFileTextProvider>(".pdf");
    }
}
