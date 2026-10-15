using Grove.Data.Documents;

namespace Grove.Documents;

/// <summary>
/// An <see cref="IDocumentManager{TDocument}"/> using a given type of <see cref="IDocumentStore"/>.
/// </summary>
public interface IDocumentManager<TDocumentStore, TDocument> : IDocumentManager<TDocument>
    where TDocumentStore : IDocumentStore where TDocument : class, IDocument, new();
