using Grove.Routing;
using Grove.Themes;
using NSubstitute;
using Grove.Admin;
using Grove.DisplayManagement.Extensions;
using Grove.DisplayManagement.Manifest;
using Grove.Environment.Extensions;
using Grove.Environment.Extensions.Features;
using Grove.Environment.Shell;
using Grove.Modules.Manifest;
using Grove.Themes.Services;
using Xunit;

namespace Grove.Server.Tests;

// Theme compatibility (docs/shells-and-themes.md › Theme compatibility): a feature's shell
// contract is satisfied by a Grove Blazor theme or any fork of one, and a pending theme
// change or reset is evaluated against the bucket it changes.
public class ShellCompatibilityTests
{
    private const string GroveAdmin = "Grove.AdminTheme";
    private const string ClassicAdmin = "TheAdmin";
    private const string BrandedAdmin = "Acme.Admin";
    private const string GroveMember = "Grove.MemberTheme";
    private const string AdminFeature = "Acme.Ledger";
    private const string MemberFeature = "Acme.Portal";

    private readonly Dictionary<string, IExtensionInfo> _themes = new(StringComparer.OrdinalIgnoreCase);

    public ShellCompatibilityTests()
    {
        Theme(GroveAdmin, ["admin", ThemeBuckets.GroveBlazorTag]);
        Theme(ClassicAdmin, ["admin"]);
        Theme(BrandedAdmin, ["admin"], baseTheme: GroveAdmin);
        Theme(GroveMember, ["member", ThemeBuckets.GroveBlazorTag]);
    }

    // An assembly belongs to the module or feature with the longest matching id, never to a
    // shorter prefix (the always-enabled "Grove" must not inherit "Grove.Members"' client, and
    // a library named for a feature belongs to that feature, not to its module).
    [Theory]
    [InlineData("Grove.Members.Member.BlazorWasm", "Grove.Members")]
    [InlineData("Grove.Contents.PartLists.BlazorWasm", "Grove.Contents.PartLists")]
    [InlineData("Grove.Contents.BlazorWasm", "Grove.Contents")]
    [InlineData("Grove.Other.BlazorWasm", "Grove")]
    [InlineData("Acme.Ledger.BlazorWasm", null)]
    public void ClientAssemblyBelongsToTheLongestMatchingModule(string assemblyName, string? expected)
    {
        string[] modules = ["Grove", "Grove.Members", "Grove.Contents", "Grove.Contents.PartLists", "Grove.Member"];

        Assert.Equal(expected, Grove.Routing.ModuleClientAssemblies.OwningExtensionId(assemblyName, modules));
    }

    [Fact]
    public async Task GroveBlazorAdminThemeSatisfiesAnAdminContract()
    {
        var service = Service(activeAdmin: GroveAdmin);

        Assert.Empty(await service.GetIncompatibilitiesAsync([AdminFeature]));
    }

    [Fact]
    public async Task ForkOfTheGroveThemeSatisfiesTheContract()
    {
        var service = Service(activeAdmin: BrandedAdmin);

        Assert.Empty(await service.GetIncompatibilitiesAsync([AdminFeature]));
    }

    [Fact]
    public async Task PendingChangeToAClassicThemeBreaksAdminFeatures()
    {
        var service = Service(activeAdmin: GroveAdmin);

        var broken = await service.GetIncompatibilitiesAsync(pending: new PendingThemeChange(ShellId.Admin, ClassicAdmin));

        var incompatibility = Assert.Single(broken);
        Assert.Equal(AdminFeature, incompatibility.FeatureId);
        Assert.Equal(ShellId.Admin, incompatibility.Bucket);
        Assert.Equal(ClassicAdmin, incompatibility.ActiveThemeId);
    }

    [Fact]
    public async Task ResettingABucketLeavesItsFeaturesWithNoTheme()
    {
        var service = Service(activeAdmin: GroveAdmin);

        var broken = await service.GetIncompatibilitiesAsync(pending: new PendingThemeChange(ShellId.Admin, null));

        var incompatibility = Assert.Single(broken);
        Assert.Null(incompatibility.ActiveThemeId);
    }

    [Fact]
    public async Task PendingChangeOnlyAffectsItsOwnBucket()
    {
        var service = Service(activeAdmin: GroveAdmin);

        Assert.Empty(await service.GetIncompatibilitiesAsync(pending: new PendingThemeChange(ShellId.Site, null)));
    }

    [Fact]
    public async Task MemberContractIsCheckedAgainstTheMemberThemeSetting()
    {
        var service = Service(activeAdmin: GroveAdmin, activeMember: ClassicAdmin);

        var incompatibility = Assert.Single(await service.GetIncompatibilitiesAsync());
        Assert.Equal(MemberFeature, incompatibility.FeatureId);
        Assert.Equal(ShellId.Member, incompatibility.Bucket);
    }

    [Fact]
    public async Task RequiredBucketsAreTheEnabledFeaturesContracts()
    {
        var service = Service(activeAdmin: GroveAdmin);

        var buckets = await service.GetRequiredBucketsAsync();

        Assert.Equal([ShellId.Admin, ShellId.Member], buckets.Order());
    }

    private ShellCompatibilityService Service(string activeAdmin, string activeMember = GroveMember)
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
            [Provider(AdminFeature, ShellId.Admin), Provider(MemberFeature, ShellId.Member)]);
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

    private static IShellContractProvider Provider(string featureId, ShellId bucket)
    {
        var provider = Substitute.For<IShellContractProvider>();
        provider.FeatureId.Returns(featureId);
        provider.GetContracts().Returns([new ShellContract(bucket)]);
        return provider;
    }
}
