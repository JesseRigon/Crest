using Grove.Contents.Management;
using Grove.Contents.Management.Metadata.Models;

namespace Grove.Data.Indexing;

/// <summary>
/// An implementation of <see cref="IContentFieldIndexHandler"/> is able to take part in the rendering of
/// a <see cref="ContentField"/> instance.
/// </summary>
public interface IContentFieldIndexHandler
{
    Task BuildIndexAsync(ContentFieldSet contentFieldSet, ContentTypeFieldSetDefinition typeFieldSetDefinition, ContentFieldDefinition partFieldDefinition, BuildDocumentIndexContext context, IContentIndexSettings settings);
}
