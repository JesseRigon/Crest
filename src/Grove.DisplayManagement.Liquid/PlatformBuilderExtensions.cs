namespace Microsoft.Extensions.DependencyInjection;

public static class PlatformBuilderExtensions
{
    /// <summary>
    /// Adds tenant level services for managing liquid view template files.
    /// </summary>
    [Obsolete("This class is deprecated and will be removed in the upcoming major release.")]
    public static PlatformBuilder AddLiquidViews(this PlatformBuilder builder)
    {
        builder.ConfigureServices(services => services.AddLiquidCoreServices());

        return builder;
    }
}
