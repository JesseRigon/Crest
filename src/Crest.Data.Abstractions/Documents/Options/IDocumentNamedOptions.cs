namespace Crest.Documents.Options;

public interface IDocumentNamedOptions
{
    string CacheKey { get; set; }
    string CacheIdKey { get; set; }
}
