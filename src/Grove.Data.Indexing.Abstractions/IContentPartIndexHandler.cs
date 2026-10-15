using Grove.Contents.Management;
using Grove.Contents.Management.Metadata.Models;

namespace Grove.Data.Indexing;

/// <summary>
/// An implementation of <see cref="IContentPartIndexHandler"/> is able to take part in the rendering of
/// a <see cref="ContentPart"/> instance.
/// </summary>
public interface IContentPartIndexHandler
{
    Task BuildIndexAsync(ContentPart contentPart, ContentTypePartDefinition typePartDefinition, BuildDocumentIndexContext context, IContentIndexSettings settings);
}
