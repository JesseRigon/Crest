using Crest.Scripting;
using Crest.Scripting.Files;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides an extension method for <see cref="PlatformBuilder"/>.
/// </summary>
public static partial class PlatformBuilderExtensions
{
    /// <summary>
    /// Adds scripting services.
    /// </summary>
    /// <param name="builder">The <see cref="PlatformBuilder"/>.</param>
    public static PlatformBuilder AddScripting(this PlatformBuilder builder)
    {
        builder.ApplicationServices.AddSingleton<IGlobalMethodProvider, CommonGeneratorMethods>();
        builder.ApplicationServices.AddSingleton<IScriptingEngine, FilesScriptEngine>();

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IScriptingManager, DefaultScriptingManager>();
        });

        return builder;
    }
}
