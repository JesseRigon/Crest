using Microsoft.Extensions.Options;
using Crest.Data.Documents;
using Crest.Documents;
using Crest.Documents.Options;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class PlatformBuilderExtensions
{
    /// <summary>
    /// Adds tenant level services to keep in sync any single <see cref="IDocument"/> between an <see cref="IDocumentStore"/> and a multi level cache.
    /// </summary>
    public static PlatformBuilder AddDocumentManagement(this PlatformBuilder builder)
    {
        return builder.ConfigureServices(services =>
        {
            services.AddSingleton(typeof(IDocumentManager<>), typeof(DocumentManager<>));
            services.AddSingleton(typeof(IVolatileDocumentManager<>), typeof(VolatileDocumentManager<>));
            services.AddSingleton(typeof(IDocumentManager<,>), typeof(DocumentManager<,>));
            services.AddSingleton<IConfigureOptions<DocumentOptions>, DocumentOptionsSetup>();
            services.AddSingleton(typeof(IDocumentEntityManager<>), typeof(DocumentEntityManager<>));
            services.AddSingleton(typeof(IVolatileDocumentEntityManager<>), typeof(VolatileDocumentEntityManager<>));
            services.AddSingleton(typeof(IDocumentEntityManager<,>), typeof(DocumentEntityManager<,>));
        });
    }
}
