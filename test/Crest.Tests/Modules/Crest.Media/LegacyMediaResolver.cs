using Crest.Environment.Shell;
using Crest.Media;
using Crest.Media.Files;
using Crest.Media.Library;
using Crest.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Tests.Modules.Crest.Media;

/// <summary>A reference resolver for a tenant whose media library is not imported yet: every path
/// keeps its <c>/media</c> URL from the media store (ruling F1's compatibility form).</summary>
internal static class LegacyMediaResolver
{
    public static IMediaReferenceResolver For(IMediaFileStore fileStore, IHttpContextAccessor httpContextAccessor) => new MediaReferenceResolver(
        Mock.Of<IMediaObjectService>(),
        Mock.Of<IMediaLibrary>(),
        new ServiceCollection().AddSingleton(fileStore).BuildServiceProvider(),
        httpContextAccessor,
        new ShellSettings { Name = "Default" },
        Mock.Of<IClock>(),
        Options.Create(new MediaOptions { AssetsRequestPath = "/media" }),
        Options.Create(new MediaLibraryOptions()));
}
