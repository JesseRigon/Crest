using System.Text.Json.Nodes;
using Grove.Data.Documents;

namespace Grove.Documents;

/// <summary>
/// A <see cref="Document"/> being an <see cref="IDocumentEntity"/>.
/// </summary>
public class DocumentEntity : Document, IDocumentEntity
{
    public JsonObject Properties { get; set; } = [];
}
