using Crest.ContentManagement;
using Crest.ContentManagement.Metadata.Models;

namespace Crest.Indexing;

/// <summary>
/// An implementation of <see cref="IContentFieldIndexHandler"/> is able to take part in the rendering of
/// a <see cref="ContentField"/> instance.
/// </summary>
public interface IContentFieldIndexHandler
{
    Task BuildIndexAsync(ContentPart contentPart, ContentTypePartDefinition typePartDefinition, ContentPartFieldDefinition partFieldDefinition, BuildDocumentIndexContext context, IContentIndexSettings settings);
}
