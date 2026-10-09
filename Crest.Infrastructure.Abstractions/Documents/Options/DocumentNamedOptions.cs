namespace Crest.Documents.Options;

public class DocumentNamedOptions : DocumentOptionsBase, IDocumentNamedOptions
{
    public string CacheKey { get; set; }
    public string CacheIdKey { get; set; }
}
