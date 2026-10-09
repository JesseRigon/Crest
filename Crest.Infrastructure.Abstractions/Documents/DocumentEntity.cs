using System.Text.Json.Nodes;
using Crest.Data.Documents;

namespace Crest.Documents;

/// <summary>
/// A <see cref="Document"/> being an <see cref="IDocumentEntity"/>.
/// </summary>
public class DocumentEntity : Document, IDocumentEntity
{
    public JsonObject Properties { get; set; } = [];
}
