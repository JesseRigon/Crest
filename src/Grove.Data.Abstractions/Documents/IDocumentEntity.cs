using Grove.Data.Documents;
using Grove.Entities;

namespace Grove.Documents;

/// <summary>
/// An <see cref="IDocument"/> being an <see cref="IEntity"/>.
/// </summary>
public interface IDocumentEntity : IDocument, IEntity;
