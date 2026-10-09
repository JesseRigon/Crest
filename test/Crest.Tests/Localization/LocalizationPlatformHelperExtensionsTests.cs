using Crest.Localization;

namespace Crest.Tests.Localization;

public class LocalizationPlatformHelperExtensionsTests
{
    [Fact]
    public void GetJSLocalizations_GroupsIsNull_ThrowsArgumentNullException()
    {
        var platformHelper = CreatePlatformHelper([]);

        Assert.Throws<ArgumentNullException>(() => platformHelper.GetJSLocalizations(null));
    }

    [Fact]
    public void GetJSLocalizations_CallsLocalizersPerRequestedGroupAndMergesResults_Succeeds()
    {
        var firstLocalizer = new TrackingJSLocalizer(new Dictionary<string, IDictionary<string, string>>
        {
            ["shared"] = new Dictionary<string, string>
            {
                ["Cancel"] = "Cancel",
                ["Save"] = "Save",
            },
        });

        var secondLocalizer = new TrackingJSLocalizer(new Dictionary<string, IDictionary<string, string>>
        {
            ["feature"] = new Dictionary<string, string>
            {
                ["Save"] = "Store",
                ["Delete"] = "Delete",
            },
        });

        var platformHelper = CreatePlatformHelper([firstLocalizer, secondLocalizer]);

        var result = platformHelper.GetJSLocalizations("shared", "feature");

        Assert.Equal(["shared", "feature"], firstLocalizer.RequestedGroups);
        Assert.Equal(["shared", "feature"], secondLocalizer.RequestedGroups);
        Assert.Equal("Cancel", result["Cancel"]);
        Assert.Equal("Store", result["Save"]);
        Assert.Equal("Delete", result["Delete"]);
    }

    [Fact]
    public void GetJSLocalizations_Default_IgnoressNullResults()
    {
        var platformHelper = CreatePlatformHelper([new NullReturningJSLocalizer()]);

        var result = platformHelper.GetJSLocalizations("shared");

        Assert.Empty(result);
    }

    private static TestPlatformHelper CreatePlatformHelper(IEnumerable<IJSLocalizer> localizers)
    {
        var services = new ServiceCollection();

        foreach (var localizer in localizers)
        {
            services.AddScoped<IJSLocalizer>(_ => localizer);
        }

        var httpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
        };

        return new TestPlatformHelper(httpContext);
    }

    private sealed class TestPlatformHelper : IPlatformHelper
    {
        public TestPlatformHelper(HttpContext httpContext)
        {
            HttpContext = httpContext;
        }

        public HttpContext HttpContext { get; }
    }

    private sealed class TrackingJSLocalizer : IJSLocalizer
    {
        private readonly IDictionary<string, IDictionary<string, string>> _localizations;

        public TrackingJSLocalizer(IDictionary<string, IDictionary<string, string>> localizations)
        {
            _localizations = localizations;
        }

        public List<string> RequestedGroups { get; } = [];

        public IDictionary<string, string> GetLocalizations(string group)
        {
            RequestedGroups.Add(group);

            return _localizations.TryGetValue(group, out var localizations) ? localizations : new Dictionary<string, string>();
        }
    }

    private sealed class NullReturningJSLocalizer : IJSLocalizer
    {
        public IDictionary<string, string> GetLocalizations(string group) => null;
    }
}
