using Grove.Display.Management.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Infrastructure.Server.Tests;

// The display tree services (DisplayTreeBuilder -> DisplayTreeRenderer), over a registry built
// from a test catalog.
internal static class DisplayTreeTestServices
{
    public static IServiceCollection AddTestDisplayTree(this IServiceCollection services, params ComponentDescriptor[] components)
    {
        services.AddSingleton<IComponentRegistry>(new ComponentRegistry([new TestCatalog(components)]));
        services.AddScoped<IDisplayTreeBuilder, DisplayTreeBuilder>();
        services.AddOptions<DisplayTreeOptions>();
        return services;
    }

    /// <summary>A component whose schema is one string <c>Text</c> parameter.</summary>
    public static ComponentDescriptor WithText(string name, Type type) => new(
        name,
        type,
        [new ParameterDescriptor("Text", typeof(string), ParameterKind.String, "text")],
        [],
        new HashSet<ShellKind>());

    private sealed class TestCatalog(IEnumerable<ComponentDescriptor> components) : IComponentCatalog
    {
        public IEnumerable<ComponentDescriptor> Describe() => components;
    }
}
