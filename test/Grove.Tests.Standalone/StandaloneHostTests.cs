using System.Text.Json.Nodes;
using Grove.Data.Global.Services;
using Grove.Environment.Extensions;
using Grove.Environment.Shell;
using Grove.Data.Types.Money.Abstractions;
using Grove.Parties.Services;
using Grove.Security;
using Grove.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Grove.Tests.Standalone;

// Grove stands alone (docs/architecture.md › Still to build, item 5): a host with the
// application layer and no line-of-business module installs fresh, and the seams those
// modules would fill answer with Grove's own defaults.
public sealed class StandaloneHostTests(StandaloneHost host) : IClassFixture<StandaloneHost>
{
    private static readonly string[] ApplicationFeatures =
    [
        "Grove", "Grove.Contents.OptionLists", "Grove.Data.Global", "Grove.Data.Regions", "Grove.Data.Types.Money",
        "Grove.Parties", "Grove.Access.Members", "Grove.Workflows", "Grove.Workflows.Contents",
    ];

    [Fact]
    public void TheFreshInstallLivesUnderItsOwnDataRoot()
    {
        Assert.True(Directory.Exists(Path.Combine(host.DataRoot, "Sites", ShellSettings.DefaultShellName)), "the Default tenant's folder is under Grove:DataRoot");
        Assert.True(File.Exists(Path.Combine(host.DataRoot, "tenants.json")), "tenants.json is under Grove:DataRoot");
        Assert.True(Directory.Exists(Path.Combine(host.DataRoot, GlobalStore.GlobalFolderName)), "the global store is under Grove:DataRoot");
    }

    [Fact]
    public async Task TheApplicationLayerIsEnabledAndNothingDownstreamIs()
    {
        await host.InDefaultTenantAsync(async services =>
        {
            var enabled = (await services.GetRequiredService<IShellFeaturesManager>().GetEnabledFeaturesAsync())
                .Select(feature => feature.Id)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var feature in ApplicationFeatures)
            {
                Assert.Contains(feature, enabled);
            }

            var application = typeof(StandaloneHost).Assembly.GetName().Name;
            Assert.All(enabled, id => Assert.True(
                id.StartsWith("Grove", StringComparison.Ordinal) || id is "Application.Default" || id == application,
                $"'{id}' is not one of Grove's features"));
        });
    }

    [Fact]
    public async Task TheRegistriesAnswerWithGrovesOwnEntries()
    {
        await host.InDefaultTenantAsync(async services =>
        {
            var parties = services.GetRequiredService<PartyTypeCatalog>();
            Assert.Contains(parties.Kinds, kind => kind.Key == "contacts");
            Assert.Contains(parties.Kinds, kind => kind.Key == "organizations");

            var sources = services.GetServices<IOptionSourceProvider>().Select(source => source.Key).ToArray();
            Assert.Contains("currency", sources);

            var rates = services.GetRequiredService<IExchangeRateRegistry>();
            var identity = await rates.GetRateAsync("EUR", "EUR", new DateOnly(2026, 1, 1), TestContext.Current.CancellationToken);
            Assert.Equal(1m, identity!.Rate);
            // No feed and an empty manual table: the default behaviour is Continue, so no rate.
            Assert.Null(await rates.GetRateAsync("USD", "EUR", new DateOnly(2026, 1, 1), TestContext.Current.CancellationToken));
        });
    }

    [Fact]
    public async Task TheWorkflowRolesShipWithTheFeature()
    {
        await host.InDefaultTenantAsync(async services =>
        {
            var roles = services.GetRequiredService<RoleManager<IRole>>();
            Assert.NotNull(await roles.FindByNameAsync("WorkflowEditor"));
            Assert.NotNull(await roles.FindByNameAsync("WorkflowViewer"));
        });
    }

    [Fact]
    public async Task RecordNumbersAreGapFreePerSequence()
    {
        long first = 0, second = 0, other = 0;
        await host.InDefaultTenantAsync(async services => first = await services.GetRequiredService<IRecordNumbering>().NextAsync("standalone:a"));
        await host.InDefaultTenantAsync(async services => second = await services.GetRequiredService<IRecordNumbering>().NextAsync("standalone:a"));
        await host.InDefaultTenantAsync(async services => other = await services.GetRequiredService<IRecordNumbering>().NextAsync("standalone:b"));

        Assert.Equal(1, first);
        Assert.Equal(2, second);
        Assert.Equal(1, other);
    }

    [Fact]
    public async Task TheSiteAndTheAdminLoginServe()
    {
        // No home route is set: "/" is the site theme's default home, not a miss.
        using var root = await host.Client.GetAsync(string.Empty, TestContext.Current.CancellationToken);
        root.EnsureSuccessStatusCode();
        Assert.Contains("site-home", await root.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);

        using var login = await host.Client.GetAsync("login", TestContext.Current.CancellationToken);
        login.EnsureSuccessStatusCode();
    }

    // The development recipe (src/Grove.Themes.Site/Recipes/grove.dev.recipe.json) enables every
    // Grove feature: each feature this host knows is either in its feature step or in its
    // "excludedFeatures" with the reason, so a new feature cannot be left out silently.
    // tools/manifest-features.py --check makes the same comparison from the manifests' source.
    private const string RecipePath = "DevRecipe/grove.dev.recipe.json";

    [Fact]
    public void EveryFeatureIsEnabledOrExcludedWithAReason()
    {
        var recipe = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, RecipePath)))!.AsObject();
        var enabled = recipe["steps"]!.AsArray()
            .Where(step => (string?)step!["name"] == "feature")
            .SelectMany(step => step!["enable"]!.AsArray().Select(id => (string)id!))
            .ToList();
        var excluded = recipe["excludedFeatures"]!.AsObject()
            .ToDictionary(pair => pair.Key, pair => (string?)pair.Value, StringComparer.Ordinal);

        var application = typeof(StandaloneHost).Assembly.GetName().Name;
        var features = host.Services.GetRequiredService<IExtensionManager>().GetFeatures()
            .Select(feature => feature.Id)
            .Where(id => id != application && id != "Application.Default")
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(enabled.Count, enabled.Distinct(StringComparer.Ordinal).Count());
        Assert.All(excluded, pair => Assert.False(string.IsNullOrWhiteSpace(pair.Value), $"'{pair.Key}' is excluded without a reason"));

        var missing = features.Where(id => !enabled.Contains(id) && !excluded.ContainsKey(id)).Order(StringComparer.Ordinal).ToList();
        Assert.True(missing.Count == 0, "Features neither enabled nor excluded by the dev recipe: " + string.Join(", ", missing));

        var both = enabled.Where(excluded.ContainsKey).ToList();
        Assert.True(both.Count == 0, "Features both enabled and excluded: " + string.Join(", ", both));

        var unknown = enabled.Concat(excluded.Keys).Where(id => !features.Contains(id)).ToList();
        Assert.True(unknown.Count == 0, "Recipe features no module declares: " + string.Join(", ", unknown));
    }
}
