using Grove.Workflows.Features.Services;
using Grove.Workflows.Expressions.JavaScript.Features;
using Grove.Workflows.Expressions.JavaScript.Options;

// ReSharper disable once CheckNamespace
namespace Grove.Workflows.Extensions;

/// <summary>
/// Adds extensions to <see cref="IModule"/> that installs the <see cref="JavaScriptFeature"/> feature.
/// </summary>
public static class ModuleExtensions
{
    /// <summary>
    /// Setup the <see cref="JavaScriptFeature"/> feature.
    /// </summary>
    public static IModule UseJavaScript(this IModule module, Action<JavaScriptFeature>? configure = default)
    {
        module.Configure(configure);
        return module;
    }

    /// <summary>
    /// Setup the <see cref="JavaScriptFeature"/> feature.
    /// </summary>
    public static IModule UseJavaScript(this IModule module, Action<JintOptions> configureJintOptions)
    {
        return module.UseJavaScript(javaScript => javaScript.ConfigureJintOptions(configureJintOptions));
    }
}