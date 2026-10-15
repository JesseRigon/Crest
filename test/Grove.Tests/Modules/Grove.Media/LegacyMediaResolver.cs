using Grove.Environment.Shell;
using Grove.Media;
using Grove.Media.Files;
using Grove.Media.Library;
using Grove.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Tests.Modules.Grove.Media;

/// <summary>A reference resolver for a tenant whose media library is not imported yet: every path
/// keeps its <c>/media</c> URL from the media store (ruling F1's compatibility form).</summary>
internal static class LegacyMediaResolver
{
    public static IMediaReferenceResolver For(IMediaFileStore fileStore, IHttpContextAccessor httpContextAccessor) => new MediaReferenceResolver(
        Mock.Of<IMediaLibrary>(),
        new ServiceCollection().AddSingleton(fileStore).AddSingleton(Mock.Of<IMediaObjectService>()).BuildServiceProvider(),
        httpContextAccessor,
        new ShellSettings { Name = "Default" },
        Mock.Of<IClock>(),
        Options.Create(new MediaOptions { AssetsRequestPath = "/media" }),
        Options.Create(new MediaLibraryOptions()));
}
