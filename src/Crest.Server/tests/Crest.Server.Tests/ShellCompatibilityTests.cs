using Crest.Routing;
using Crest.Themes;
using NSubstitute;
using Crest.Admin;
using Crest.DisplayManagement.Extensions;
using Crest.DisplayManagement.Manifest;
using Crest.Environment.Extensions;
using Crest.Environment.Extensions.Features;
using Crest.Environment.Shell;
using Crest.Modules.Manifest;
using Crest.Themes.Services;
using Xunit;

namespace Crest.Server.Tests;

// Theme compatibility (docs/shells-and-themes.md › Theme compatibility): a feature's shell
// contract is satisfied by a Crest Blazor theme or any fork of one, and a pending theme
// change or reset is evaluated against the bucket it changes.
public class ShellCompatibilityTests
{
    private const string CrestAdmin = "Crest.AdminTheme";
    private const string ClassicAdmin = "TheAdmin";
    private const string BrandedAdmin = "Acme.Admin";
    private const string CrestMember = "Crest.MemberTheme";
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

    // An assembly belongs to the module with the longest matching id, never to a shorter
    // prefix (the always-enabled "Crest" must not inherit "Crest.Members"' client).
    [Theory]
    [InlineData("Crest.Members.Member.BlazorWasm", "Crest.Members")]
    [InlineData("Crest.ContentPartLists.BlazorWasm", "Crest.ContentPartLists")]
    [InlineData("Crest.Other.BlazorWasm", "Crest")]
    [InlineData("Acme.Ledger.BlazorWasm", null)]
    public void ClientAssemblyBelongsToTheLongestMatchingModule(string assemblyName, string? expected)
    {
        string[] modules = ["Crest", "Crest.Members", "Crest.ContentPartLists", "Crest.Member"];

        Assert.Equal(expected, ShellCompatibilityService.OwningExtensionId(assemblyName, modules));
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

        var broken = await service.GetIncompatibilitiesAsync(pending: new PendingThemeChange(ShellId.Admin, ClassicAdmin));

        var incompatibility = Assert.Single(broken);
        Assert.Equal(AdminFeature, incompatibility.FeatureId);
        Assert.Equal(ShellId.Admin, incompatibility.Bucket);
        Assert.Equal(ClassicAdmin, incompatibility.ActiveThemeId);
    }

    [Fact]
    public async Task ResettingABucketLeavesItsFeaturesWithNoTheme()
    {
        var service = Service(activeAdmin: CrestAdmin);

        var broken = await service.GetIncompatibilitiesAsync(pending: new PendingThemeChange(ShellId.Admin, null));

        var incompatibility = Assert.Single(broken);
        Assert.Null(incompatibility.ActiveThemeId);
    }

    [Fact]
    public async Task PendingChangeOnlyAffectsItsOwnBucket()
    {
        var service = Service(activeAdmin: CrestAdmin);

        Assert.Empty(await service.GetIncompatibilitiesAsync(pending: new PendingThemeChange(ShellId.Site, null)));
    }

    [Fact]
    public async Task MemberContractIsCheckedAgainstTheMemberThemeSetting()
    {
        var service = Service(activeAdmin: CrestAdmin, activeMember: ClassicAdmin);

        var incompatibility = Assert.Single(await service.GetIncompatibilitiesAsync());
        Assert.Equal(MemberFeature, incompatibility.FeatureId);
        Assert.Equal(ShellId.Member, incompatibility.Bucket);
    }

    [Fact]
    public async Task RequiredBucketsAreTheEnabledFeaturesContracts()
    {
        var service = Service(activeAdmin: CrestAdmin);

        var buckets = await service.GetRequiredBucketsAsync();

        Assert.Equal([ShellId.Admin, ShellId.Member], buckets.Order());
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
