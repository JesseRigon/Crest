using System.Threading;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using Grove.DisplayManagement.FileProviders;

namespace Grove.Benchmarks.Support;

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
