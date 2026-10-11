namespace Crest.Environment.Extensions.Features;

/// <summary>
/// The priority of the feature a provider type ships in (the manifest's <c>Priority</c>, the
/// module's when the feature sets none): the module priority system, read at run time. A
/// contract with several providers for one thing (who decides a resource, which module's
/// shareable kind answers for a name) asks it, so a module - Crest's own or a third party's -
/// overrides another by declaring a higher priority, and nothing depends on registration order.
/// </summary>
public interface IProviderPriority
{
    /// <summary>The priority of the feature that ships <paramref name="providerType"/>; 0 when
    /// the type belongs to no feature.</summary>
    int PriorityOf(Type providerType);
}
