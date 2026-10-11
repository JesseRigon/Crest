using Crest.FileStorage;
using Crest.Media;
using Crest.Media;
using Crest.Media.Helpers;
using Crest.Media.Services;

namespace Crest.Tests.Modules.Crest.Media;

public class MediaPlatformHelperExtensionsTests
{
    [Theory]
    [InlineData("foo bar.jpg", "/media/foo%20bar.jpg")]
    [InlineData("my folder/foo bar.jpg", "/media/my%20folder/foo%20bar.jpg")]
    [InlineData("bàr.jpeg", "/media/b%C3%A0r.jpeg")]
    [InlineData("日本語.jpg", "/media/%E6%97%A5%E6%9C%AC%E8%AA%9E.jpg")]
    [InlineData("simple.jpg", "/media/simple.jpg")]
    public async Task AssetProfileUrlAsync_Default_ReturnsUrlEncodedPath(string path, string expected)
    {
        var platformHelper = CreatePlatformHelper();

        var result = await platformHelper.AssetProfileUrlAsync(path, "nonexistent-profile");

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("foo bar.jpg", 100, "/media/foo%20bar.jpg?width=100")]
    [InlineData("bàr.jpeg", 200, "/media/b%C3%A0r.jpeg?width=200")]
    [InlineData("my folder/foo bar.jpg", 50, "/media/my%20folder/foo%20bar.jpg?width=50")]
    public async Task AssetProfileUrlAsync_Width_ReturnsUrlEncodedPathWithQueryString(string path, int width, string expected)
    {
        var platformHelper = CreatePlatformHelper();

        var result = await platformHelper.AssetProfileUrlAsync(path, "nonexistent-profile", width: width);

        Assert.Equal(expected, result);
    }

    private static TestPlatformHelper CreatePlatformHelper()
    {
        var stringLocalizerMock = new Mock<IStringLocalizer<FileSizeHelper>>();
        stringLocalizerMock.Setup(x => x[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));

        stringLocalizerMock.Setup(x => x[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns((string key, object[] args) => new LocalizedString(key, string.Format(key, args)));

        var fileStore = new DefaultMediaFileStore(
            Mock.Of<IFileStore>(),
            "/media",
            "",
            [],
            [],
            new FileSizeHelper(stringLocalizerMock.Object),
            Mock.Of<ILogger<DefaultMediaFileStore>>());

        var mediaProfileServiceMock = new Mock<IMediaProfileService>();
        mediaProfileServiceMock
            .Setup(s => s.GetMediaProfileCommands(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, string>());

        var services = new ServiceCollection();
        services.AddSingleton<IMediaFileStore>(fileStore);
        services.AddSingleton(mediaProfileServiceMock.Object);
        services.AddSingleton(Options.Create(new MediaOptions { UseTokenizedQueryString = false }));

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
}
