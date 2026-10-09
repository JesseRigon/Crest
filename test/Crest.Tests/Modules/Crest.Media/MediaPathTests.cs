using Crest.Environment.Shell;
using Crest.Environment.Shell.Configuration;
using Crest.Media;
using Crest.Media.Services;
using MediaStartup = Crest.Media.Startup;

namespace Crest.Tests.Modules.Crest.Media;

public class MediaPathTests
{
    public static TheoryData<string> InvalidAssetsPaths => new()
    {
        null,
        "",
        " ",
        ".",
        "..",
        "/",
        "/application",
        @"\application",
        @"\\server\share",
        "C:/application",
        @"C:\application",
        "C:application",
        "../../../",
        "../OtherTenant/Media",
        "Media/../../../../",
        @"Media\..\..\..\..",
        "Media/../Media2",
        "Media/./Images",
        "Media//Images",
        "Media/.. /Images",
        "Media/.../Images",
        "Media /Images",
        "Media\0",
    };

    [Theory]
    [MemberData(nameof(InvalidAssetsPaths))]
    public void Validate_InvalidAssetsPath_Fails(string assetsPath)
    {
        var options = new MediaOptions
        {
            AssetsPath = assetsPath,
            AllowedFileExtensions = [],
            RestrictedFileExtensions = [],
        };

        var result = new MediaOptionsValidator().Validate(Options.DefaultName, options);

        Assert.True(result.Failed);
        Assert.Contains("AssetsPath", result.FailureMessage);
    }

    [Theory]
    [MemberData(nameof(InvalidAssetsPaths))]
    public void GetMediaPath_InvalidAssetsPath_Throws(string assetsPath)
    {
        using var settings = new ShellSettings { Name = "Tenant" };

        Assert.Throws<ArgumentException>(() => MediaStartup.GetMediaPath(CreateShellOptions(), settings, assetsPath));
    }

    [Theory]
    [InlineData("Media", "Media")]
    [InlineData("Media/", "Media")]
    [InlineData(@"Media\", "Media")]
    [InlineData("Assets/Media", "Assets/Media")]
    [InlineData(@"Assets\Media", "Assets/Media")]
    [InlineData("My Assets/Media", "My Assets/Media")]
    public void GetMediaPath_ValidAssetsPath_ResolvesInsideEachTenant(string assetsPath, string relativePath)
    {
        var shellOptions = CreateShellOptions();
        var options = new MediaOptions
        {
            AssetsPath = assetsPath,
            AllowedFileExtensions = [],
            RestrictedFileExtensions = [],
        };

        Assert.True(new MediaOptionsValidator().Validate(Options.DefaultName, options).Succeeded);

        foreach (var tenant in new[] { "Default", "OtherTenant" })
        {
            using var settings = new ShellSettings { Name = tenant };
            var expected = Path.GetFullPath(Path.Combine(
                shellOptions.ShellsApplicationDataPath, shellOptions.ShellsContainerName, tenant, relativePath));

            Assert.Equal(expected, MediaStartup.GetMediaPath(shellOptions, settings, assetsPath));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResolveMediaServices_InvalidConfiguredRoot_FailsOptionsValidation(bool resolveFileProvider)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Crest_Media:AssetsPath"] = "../../../../",
                ["Crest_Media:AllowedFileExtensions:0"] = ".dll",
            })
            .Build();
        var shellConfiguration = new Mock<IShellConfiguration>();
        shellConfiguration.Setup(c => c.GetSection("Crest_Media"))
            .Returns(configuration.GetSection("Crest_Media"));

        var services = new ServiceCollection();
        services.AddSingleton(shellConfiguration.Object);
        using var settings = new ShellSettings { Name = "Tenant" };
        services.AddSingleton(settings);
        services.Configure<ShellOptions>(options =>
        {
            options.ShellsApplicationDataPath = CreateShellOptions().ShellsApplicationDataPath;
            options.ShellsContainerName = "Sites";
        });
        new MediaStartup().ConfigureServices(services);

        using var provider = services.BuildServiceProvider();
        var exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService(resolveFileProvider ? typeof(IMediaFileProvider) : typeof(IMediaFileStore)));

        Assert.Contains("AssetsPath", exception.Message);
    }

    private static ShellOptions CreateShellOptions() => new()
    {
        ShellsApplicationDataPath = Path.Combine(Path.GetTempPath(), "media-path-tests", "App_Data"),
        ShellsContainerName = "Sites",
    };
}
