using Crest.AdminMenu.Services;
using Crest.ContentManagement.Metadata;
using Crest.Contents;
using Crest.Contents.Services;
using Crest.Environment.Extensions;
using Crest.Environment.Extensions.Features;
using Crest.Environment.Shell;
using Crest.Environment.Shell.Builders;
using Crest.Environment.Shell.Descriptor.Models;
using Crest.Localization.Data;
using Crest.Modules;
using StartupBase = Crest.Modules.StartupBase;

namespace Crest.DataLocalization.Services.Tests;

public sealed class DataLocalizationStartupTests
{
    [Theory]
    [InlineData(false, false, 0)]
    [InlineData(false, true, 0)]
    [InlineData(true, false, 2)]
    [InlineData(true, true, 3)]
    public async Task ConfigureServices_FeatureCombinations_ResolvesExpectedProviders(
        bool dataLocalizationEnabled,
        bool adminMenuEnabled,
        int expectedProviderCount)
    {
        var contentsFeature = Mock.Of<IFeatureInfo>(feature => feature.Id == "Crest.Contents");
        var extensionManager = new Mock<IExtensionManager>();
        extensionManager.Setup(manager => manager.LoadFeaturesAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync([contentsFeature]);
        extensionManager.Setup(manager => manager.GetFeatures()).Returns([contentsFeature]);

        // Discover the Contents module's data-localization startups so the real feature
        // composition applies their RequireFeatures attributes before registration.
        var startupTypes = typeof(DataLocalizationStartup).Assembly.GetExportedTypes()
            .Where(type => typeof(StartupBase).IsAssignableFrom(type)
                && RequireFeaturesAttribute.GetRequiredFeatureNamesForType(type)
                    .Contains("Crest.DataLocalization"))
            .ToArray();
        var typeFeatureProvider = new Mock<ITypeFeatureProvider>();
        typeFeatureProvider.Setup(provider => provider.GetTypesForFeature(contentsFeature)).Returns(startupTypes);

        var descriptor = new ShellDescriptor
        {
            Features = [new ShellFeature { Id = contentsFeature.Id }],
        };
        if (dataLocalizationEnabled)
        {
            descriptor.Features.Add(new ShellFeature { Id = "Crest.DataLocalization" });
        }

        if (adminMenuEnabled)
        {
            descriptor.Features.Add(new ShellFeature { Id = "Crest.AdminMenu" });
        }

        var strategy = new CompositionStrategy(extensionManager.Object, typeFeatureProvider.Object, Mock.Of<ILogger<CompositionStrategy>>());
        var blueprint = await strategy.ComposeAsync(new ShellSettings { Name = "Test" }, descriptor);
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IContentDefinitionManager>());
        if (adminMenuEnabled)
        {
            services.AddSingleton(Mock.Of<IAdminMenuAccessor>());
        }

        foreach (var startupType in blueprint.Dependencies.Keys)
        {
            ((StartupBase)Activator.CreateInstance(startupType)).ConfigureServices(services);
        }

        using var serviceProvider = services.BuildServiceProvider(validateScopes: true);
        using var scope = serviceProvider.CreateScope();
        // The Data Localization admin controller resolves this same provider collection.
        var providers = scope.ServiceProvider.GetServices<ILocalizationDataProvider>().ToArray();

        Assert.Equal(expectedProviderCount, providers.Length);
        if (dataLocalizationEnabled)
        {
            Assert.Single(providers.OfType<ContentTypeDataLocalizationProvider>());
            Assert.Single(providers.OfType<ContentFieldDataLocalizationProvider>());
        }

        Assert.Equal(dataLocalizationEnabled && adminMenuEnabled,
            providers.Any(provider => provider is ContentTypesAdminNodeDataLocalizationProvider));
    }
}
