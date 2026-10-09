using Crest.Environment.Extensions.Features;
using Crest.Environment.Shell.Descriptor.Models;

namespace Crest.Environment.Shell.Builders.Models;

/// <summary>
/// Contains the information necessary to initialize an IoC container
/// for a particular tenant. This model is created by the ICompositionStrategy
/// and is passed into the IShellContainerFactory.
/// </summary>
public class ShellBlueprint
{
    public ShellSettings Settings { get; set; }
    public ShellDescriptor Descriptor { get; set; }

    public IDictionary<Type, IEnumerable<IFeatureInfo>> Dependencies { get; set; }
}
