using Crest.Entities;
using Crest.Entities.Scripting;
using Crest.Scripting;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides an extension method for <see cref="PlatformBuilder"/>.
/// </summary>
public static partial class PlatformBuilderExtensions
{
    /// <summary>
    /// Adds IdGeneration services.
    /// </summary>
    /// <param name="builder">The <see cref="PlatformBuilder"/>.</param>
    public static PlatformBuilder AddIdGeneration(this PlatformBuilder builder)
    {
        var services = builder.ApplicationServices;

        services.AddSingleton<IIdGenerator, DefaultIdGenerator>();
        services.AddSingleton<IGlobalMethodProvider, IdGeneratorMethod>();

        return builder;
    }
}
