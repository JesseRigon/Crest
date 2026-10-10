using Crest.Data.Documents;

namespace Crest.Documents;

/// <summary>
/// An <see cref="IDocumentEntityManager{TDocumentEntity}"/> using a given type of <see cref="IDocumentStore"/>.
/// </summary>
public interface IDocumentEntityManager<TDocumentStore, TDocumentEntity> : IDocumentEntityManager<TDocumentEntity>
    where TDocumentStore : IDocumentStore where TDocumentEntity : class, IDocumentEntity, new();
