namespace Crest.Documents.Options;

public interface IDocumentSharedOptions
{
    TimeSpan? FailoverRetryLatency { get; set; }
}
