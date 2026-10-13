using Crest.DisplayManagement.Components;
using Crest.Environment.Extensions;
using Crest.Environment.Extensions.Features;
using Crest.Environment.Shell.Builders.Models;
using Crest.Environment.Shell.Descriptor.Models;

#nullable enable

namespace Crest.Tests.DisplayManagement.Components;

public class ComponentRegistryTests
{
    private static ComponentDescriptor Component(string name, Type? type = null, params ShellKind[] shells) =>
        new(name, type, [], [], new HashSet<ShellKind>(shells));

    private static ComponentRegistry Registry(ShellKind? shell, params ComponentDescriptor[] components) =>
        new([new ListCatalog(components)], shell);

    [Fact]
    public void ShellFilterKeepsComponentsOfThatShellAndShellNeutralOnes()
    {
        var registry = Registry(
            ShellKind.Member,
            Component("AdminOnly", shells: ShellKind.Admin),
            Component("MemberOnly", shells: ShellKind.Member),
            Component("Shared", shells: [ShellKind.Admin, ShellKind.Site, ShellKind.Member]),
            Component("Undeclared"));

        Assert.Equal(new[] { "MemberOnly", "Shared", "Undeclared" }, registry.Components.Select(component => component.Name).Order());
        Assert.False(registry.TryGet("AdminOnly", out _));
    }

    [Fact]
    public void NoShellKeepsEverything()
    {
        var registry = Registry(null, Component("AdminOnly", shells: ShellKind.Admin), Component("SiteOnly", shells: ShellKind.Site));

        Assert.Equal(2, registry.Components.Count);
    }

    [Fact]
    public void LaterCatalogOverridesAName()
    {
        var registry = new ComponentRegistry(
        [
            new ListCatalog([Component("Card", typeof(TestPlain))]),
            new ListCatalog([Component("card", typeof(TestCard))]),
        ]);

        Assert.True(registry.TryGet("CARD", out var card));
        Assert.Equal(typeof(TestCard), card.Type);
        Assert.Single(registry.Components);
    }

    [Fact]
    public void TryResolvePrefersTheMostSpecificAlternate()
    {
        var registry = Registry(null, Component("Content"), Component("Content__BlogPost"), Component("Content__Summary"));

        // Alternates are ordered least to most specific; the last registered one wins.
        Assert.True(registry.TryResolve("Content", ["Content__Summary", "Content__BlogPost", "Content__BlogPost__Summary"], out var resolved));
        Assert.Equal("Content__BlogPost", resolved.Name);
    }

    [Fact]
    public void TryResolveFallsBackToTheTypeSegments()
    {
        var registry = Registry(null, Component("Content"), Component("Content__BlogPost"));

        Assert.True(registry.TryResolve("Content__BlogPost__Summary", [], out var resolved));
        Assert.Equal("Content__BlogPost", resolved.Name);

        Assert.True(registry.TryResolve("Content__Article__Summary", ["Content__Unknown"], out resolved));
        Assert.Equal("Content", resolved.Name);

        Assert.False(registry.TryResolve("Widget__Hero", ["Widget__Hero__Footer"], out _));
    }

    [Fact]
    public void TryResolveRespectsTheShellFilter()
    {
        var registry = Registry(ShellKind.Site, Component("Content"), Component("Content__Page", shells: ShellKind.Admin));

        Assert.True(registry.TryResolve("Content__Page", [], out var resolved));
        Assert.Equal("Content", resolved.Name);
    }

    [Fact]
    public void ComponentOfADisabledFeatureIsAbsent()
    {
        var enabled = Feature("Test.Enabled");
        var disabled = Feature("Test.Disabled");
        var manager = Manager(
            enabledFeatures: [enabled],
            allFeatures: [enabled, disabled],
            blueprintTypes: new() { [typeof(TestCard)] = [enabled] },
            featureTypes: new() { [typeof(TestPlain)] = [disabled] });

        var registry = manager.GetRegistry(ShellKind.Site);

        Assert.True(registry.TryGet("Test__Card", out var card));
        Assert.Equal("Test.Enabled", card.Feature);

        // TestPlain is shipped by a feature the tenant has not enabled.
        Assert.False(registry.TryGet("Test__Plain", out _));

        // TestPlainSummary belongs to no feature and no module owns this assembly: always there.
        Assert.True(registry.TryGet("Test__Plain__Summary", out var shared));
        Assert.Null(shared.Feature);
    }

    [Fact]
    public void ComponentOfALibraryFollowsTheModuleItIsNamedAfter()
    {
        // This assembly is "Crest.Tests": a module "Crest" owns it by name, as
        // Crest.Parties owns Crest.Parties.BlazorWasm.
        var owner = Feature("Crest");
        var options = new ComponentRegistryOptions().Add(typeof(TestCard).Assembly);

        var disabledOwner = Manager(enabledFeatures: [], allFeatures: [owner], extensions: [owner.Extension], options: options);
        Assert.Empty(disabledOwner.GetRegistry(ShellKind.Site).Components.Where(component => component.Name.StartsWith("Test__", StringComparison.Ordinal)));

        var enabledOwner = Manager(enabledFeatures: [owner], allFeatures: [owner], extensions: [owner.Extension], options: options);
        Assert.True(enabledOwner.GetRegistry(ShellKind.Site).TryGet("Test__Plain", out var plain));
        Assert.Equal("Crest", plain.Feature);
    }

    [Fact]
    public void LibraryLimitedToAShellIsAbsentFromTheOthers()
    {
        var options = new ComponentRegistryOptions().Add(typeof(TestCard).Assembly, ShellKind.Admin);
        var manager = Manager(enabledFeatures: [], allFeatures: [], options: options);

        Assert.True(manager.GetRegistry(ShellKind.Admin).TryGet("Test__Card", out _));
        Assert.False(manager.GetRegistry(ShellKind.Member).TryGet("Test__Card", out _));
    }

    [Fact]
    public void RegistryIsBuiltOncePerShell()
    {
        var manager = Manager(enabledFeatures: [], allFeatures: []);

        Assert.Same(manager.GetRegistry(ShellKind.Site), manager.GetRegistry(ShellKind.Site));
        Assert.NotSame(manager.GetRegistry(ShellKind.Site), manager.GetRegistry(ShellKind.Admin));
    }

    [Fact]
    public void BindConvertsStoredValuesAndReportsSchemaErrors()
    {
        var card = ComponentCatalogs.ForAssembly(typeof(TestCard).Assembly).Describe().Single(component => component.Name == "Test__Card");
        var errors = new List<string>();

        var bound = ComponentParameters.Bind(
            card,
            new Dictionary<string, object?>
            {
                ["title"] = "Hello",
                ["Count"] = "5",
                ["Tone"] = "Accent",
                ["Size"] = "large",
                ["Elevated"] = "yes",
                ["Unknown"] = "x",
            },
            errors);

        Assert.Equal("Hello", bound["Title"]);
        Assert.Equal(5, bound["Count"]);
        Assert.Equal(CardTone.Accent, bound["Tone"]);
        Assert.Equal("Large", bound["Size"]);
        Assert.False(bound.ContainsKey("Elevated"));
        Assert.Contains(errors, error => error.Contains("'Elevated'", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("'Unknown'", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("'Since' is required", StringComparison.Ordinal));
    }

    private static ComponentRegistryManager Manager(
        IFeatureInfo[] enabledFeatures,
        IFeatureInfo[] allFeatures,
        Dictionary<Type, IEnumerable<IFeatureInfo>>? blueprintTypes = null,
        Dictionary<Type, IEnumerable<IFeatureInfo>>? featureTypes = null,
        IExtensionInfo[]? extensions = null,
        ComponentRegistryOptions? options = null)
    {
        var blueprint = new ShellBlueprint
        {
            Descriptor = new ShellDescriptor { Features = [.. enabledFeatures.Select(feature => new ShellFeature(feature.Id))] },
            Dependencies = blueprintTypes ?? new Dictionary<Type, IEnumerable<IFeatureInfo>>(),
        };

        var extensionManager = new Mock<IExtensionManager>();
        extensionManager.Setup(manager => manager.GetFeatures()).Returns(allFeatures);
        extensionManager.Setup(manager => manager.GetExtensions()).Returns(extensions ?? Array.Empty<IExtensionInfo>());
        extensionManager.Setup(manager => manager.GetFeatureDependencies(It.IsAny<string>())).Returns(Array.Empty<IFeatureInfo>());

        var typeFeatureProvider = new Mock<ITypeFeatureProvider>();
        typeFeatureProvider
            .Setup(provider => provider.GetFeaturesForDependency(It.IsAny<Type>()))
            .Returns((Type type) => featureTypes is not null && featureTypes.TryGetValue(type, out var features)
                ? features
                : throw new InvalidOperationException($"Could not resolve features for type {type.Name}."));

        // Without options, the test assembly is read because blueprint types live in it.
        return new ComponentRegistryManager(blueprint, extensionManager.Object, typeFeatureProvider.Object, Options.Create(options ?? new ComponentRegistryOptions()));
    }

    private static IFeatureInfo Feature(string id)
    {
        var extension = new Mock<IExtensionInfo>();
        var feature = new Mock<IFeatureInfo>();
        extension.SetupGet(info => info.Id).Returns(id);
        extension.SetupGet(info => info.Features).Returns(() => new[] { feature.Object });
        feature.SetupGet(info => info.Id).Returns(id);
        feature.SetupGet(info => info.Extension).Returns(extension.Object);
        return feature.Object;
    }

    private sealed class ListCatalog(IEnumerable<ComponentDescriptor> components) : IComponentCatalog
    {
        public IEnumerable<ComponentDescriptor> Describe() => components;
    }
}
