using Crest.Routing;
using Crest.Themes;
using NSubstitute;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Extensions;
using OrchardCore.DisplayManagement.Manifest;
using OrchardCore.Environment.Extensions;
using OrchardCore.Environment.Extensions.Features;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules.Manifest;
using OrchardCore.Themes.Services;
using Xunit;

namespace OrchardCore.Crest.Server.Tests;

// Theme compatibility (plans/shells-and-themes.md › Theme compatibility): a feature's shell
// contract is satisfied by a Crest Blazor theme or any fork of one, and a pending theme
// change or reset is evaluated against the bucket it changes.
public class ShellCompatibilityTests
{
    private const string CrestAdmin = "OrchardCore.Crest.Admin";
    private const string ClassicAdmin = "TheAdmin";
    private const string BrandedAdmin = "Acme.Admin";
    private const string CrestMember = "OrchardCore.Crest.Member";
    private const string AdminFeature = "Acme.Ledger";
    private const string MemberFeature = "Acme.Portal";

    private readonly Dictionary<string, IExtensionInfo> _themes = new(StringComparer.OrdinalIgnoreCase);

    public ShellCompatibilityTests()
    {
        Theme(CrestAdmin, ["admin", ThemeBuckets.CrestBlazorTag]);
        Theme(ClassicAdmin, ["admin"]);
        Theme(BrandedAdmin, ["admin"], baseTheme: CrestAdmin);
        Theme(CrestMember, ["member", ThemeBuckets.CrestBlazorTag]);
    }

    [Fact]
    public async Task CrestBlazorAdminThemeSatisfiesAnAdminContract()
    {
        var service = Service(activeAdmin: CrestAdmin);

        Assert.Empty(await service.GetIncompatibilitiesAsync([AdminFeature]));
    }

    [Fact]
    public async Task ForkOfTheCrestThemeSatisfiesTheContract()
    {
        var service = Service(activeAdmin: BrandedAdmin);

        Assert.Empty(await service.GetIncompatibilitiesAsync([AdminFeature]));
    }

    [Fact]
    public async Task PendingChangeToAClassicThemeBreaksAdminFeatures()
    {
        var service = Service(activeAdmin: CrestAdmin);

        var broken = await service.GetIncompatibilitiesAsync(pending: new PendingThemeChange(RouteBucket.Admin, ClassicAdmin));

        var incompatibility = Assert.Single(broken);
        Assert.Equal(AdminFeature, incompatibility.FeatureId);
        Assert.Equal(RouteBucket.Admin, incompatibility.Bucket);
        Assert.Equal(ClassicAdmin, incompatibility.ActiveThemeId);
    }

    [Fact]
    public async Task ResettingABucketLeavesItsFeaturesWithNoTheme()
    {
        var service = Service(activeAdmin: CrestAdmin);

        var broken = await service.GetIncompatibilitiesAsync(pending: new PendingThemeChange(RouteBucket.Admin, null));

        var incompatibility = Assert.Single(broken);
        Assert.Null(incompatibility.ActiveThemeId);
    }

    [Fact]
    public async Task PendingChangeOnlyAffectsItsOwnBucket()
    {
        var service = Service(activeAdmin: CrestAdmin);

        Assert.Empty(await service.GetIncompatibilitiesAsync(pending: new PendingThemeChange(RouteBucket.Site, null)));
    }

    [Fact]
    public async Task MemberContractIsCheckedAgainstTheMemberThemeSetting()
    {
        var service = Service(activeAdmin: CrestAdmin, activeMember: ClassicAdmin);

        var incompatibility = Assert.Single(await service.GetIncompatibilitiesAsync());
        Assert.Equal(MemberFeature, incompatibility.FeatureId);
        Assert.Equal(RouteBucket.Member, incompatibility.Bucket);
    }

    [Fact]
    public async Task RequiredBucketsAreTheEnabledFeaturesContracts()
    {
        var service = Service(activeAdmin: CrestAdmin);

        var buckets = await service.GetRequiredBucketsAsync();

        Assert.Equal([RouteBucket.Admin, RouteBucket.Member], buckets.Order());
    }

    private ShellCompatibilityService Service(string activeAdmin, string activeMember = CrestMember)
    {
        var adminThemes = Substitute.For<IAdminThemeService>();
        adminThemes.GetAdminThemeAsync().Returns(_themes[activeAdmin]);

        var siteThemes = Substitute.For<ISiteThemeService>();
        siteThemes.GetSiteThemeAsync().Returns((IExtensionInfo?)null);

        var memberThemes = Substitute.For<IMemberThemeService>();
        memberThemes.GetMemberThemeAsync().Returns(_themes[activeMember]);

        var extensions = Substitute.For<IExtensionManager>();
        var missing = Missing();
        extensions.GetExtension(Arg.Any<string>()).Returns(call =>
            _themes.TryGetValue(call.Arg<string>() ?? string.Empty, out var theme) ? theme : missing);
        extensions.GetFeatures().Returns([]);

        IFeatureInfo[] enabled = [Feature(AdminFeature), Feature(MemberFeature)];
        var features = Substitute.For<IShellFeaturesManager>();
        features.GetEnabledFeaturesAsync().Returns(enabled);

        return new ShellCompatibilityService(
            adminThemes,
            siteThemes,
            memberThemes,
            extensions,
            features,
            [Provider(AdminFeature, RouteBucket.Admin), Provider(MemberFeature, RouteBucket.Member)]);
    }

    private void Theme(string id, string[] tags, string? baseTheme = null)
    {
        var manifest = Substitute.For<IManifestInfo>();
        manifest.Tags.Returns(tags);
        manifest.ModuleInfo.Returns(new ThemeAttribute { BaseTheme = baseTheme });

        var extension = Substitute.For<IExtensionInfo>();
        extension.Id.Returns(id);
        extension.Exists.Returns(true);
        extension.Manifest.Returns(manifest);

        _themes[id] = new ThemeExtensionInfo(extension);
    }

    private static IExtensionInfo Missing()
    {
        var extension = Substitute.For<IExtensionInfo>();
        extension.Id.Returns("Missing.Theme");
        extension.Exists.Returns(false);
        return extension;
    }

    private static IFeatureInfo Feature(string id)
    {
        var feature = Substitute.For<IFeatureInfo>();
        feature.Id.Returns(id);
        return feature;
    }

    private static IShellContractProvider Provider(string featureId, RouteBucket bucket)
    {
        var provider = Substitute.For<IShellContractProvider>();
        provider.FeatureId.Returns(featureId);
        provider.GetContracts().Returns([new ShellContract(bucket)]);
        return provider;
    }
}
