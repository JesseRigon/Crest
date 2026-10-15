namespace Grove.Display.Components.Modules;

/// <summary>
/// Declares a client assembly that loads at boot, whatever shell the document serves: the
/// shell runtime and a theme's services (the HTTP pipeline, the API client, the state the
/// host registers before the container is sealed). Everything else a host's WASM app ships -
/// theme UIs and module libraries - loads on demand (Grove.LazyModules).
/// </summary>
/// <remarks>
/// The lazy-module generator cannot see the app's own references (it runs before the app is
/// compiled), so the eager roots are named by this attribute rather than inferred.
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class GroveEagerAttribute : Attribute
{
}
