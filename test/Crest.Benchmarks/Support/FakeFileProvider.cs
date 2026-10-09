using System.Threading;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using Crest.DisplayManagement.FileProviders;

namespace Crest.Benchmarks.Support;

internal sealed class FakeFileProvider : IFileProvider
{
    public IDirectoryContents GetDirectoryContents(string subpath)
    {
        return new NotFoundDirectoryContents();
    }

    public IFileInfo GetFileInfo(string subpath)
    {
        return new ContentFileInfo("name", "content");
    }

    public IChangeToken Watch(string filter)
    {
        return new CancellationChangeToken(CancellationToken.None);
    }
}
