using Crest.Data.Documents;
using Crest.Entities;

namespace Crest.Documents;

/// <summary>
/// An <see cref="IDocument"/> being an <see cref="IEntity"/>.
/// </summary>
public interface IDocumentEntity : IDocument, IEntity;
