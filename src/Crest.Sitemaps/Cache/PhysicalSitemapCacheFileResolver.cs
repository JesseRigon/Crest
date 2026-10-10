using Microsoft.Extensions.FileProviders;

namespace Crest.Sitemaps.Cache;

public class PhysicalSitemapCacheFileResolver : ISitemapCacheFileResolver
{
    private readonly IFileInfo _fileInfo;

    public PhysicalSitemapCacheFileResolver(IFileInfo fileInfo)
    {
        _fileInfo = fileInfo;
    }

    public Task<Stream> OpenReadStreamAsync()
    {
        return Task.FromResult(_fileInfo.CreateReadStream());
    }
}
