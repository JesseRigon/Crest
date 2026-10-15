using Grove.Environment.Extensions.Features;
using Grove.Environment.Shell.Builders.Models;

namespace Grove.Environment.Shell.Builders;

/// <summary>The shell's <see cref="IProviderPriority"/>: the blueprint maps every type of an
/// enabled feature's assembly to that feature, and the feature carries its priority.</summary>
public sealed class BlueprintProviderPriority(ShellBlueprint blueprint) : IProviderPriority
{
    public int PriorityOf(Type providerType) =>
        blueprint.Dependencies.TryGetValue(providerType, out var features) && features.Any()
            ? features.Max(feature => feature.Priority)
            : 0;
}
