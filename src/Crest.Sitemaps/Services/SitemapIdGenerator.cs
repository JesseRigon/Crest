using Crest.Entities;

namespace Crest.Sitemaps.Services;

public class SitemapIdGenerator : ISitemapIdGenerator
{
    private readonly IIdGenerator _idGenerator;

    public SitemapIdGenerator(IIdGenerator idGenerator)
    {
        _idGenerator = idGenerator;
    }

    public string GenerateUniqueId()
    {
        return _idGenerator.GenerateUniqueId();
    }
}
