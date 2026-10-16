using Grove.Contents.Management;
using Grove.Contents.Management.Metadata.Models;

namespace Grove.Data.Indexing;

/// <summary>
/// An implementation of <see cref="IContentFieldSetIndexHandler"/> is able to take part in the rendering of
/// a <see cref="ContentFieldSet"/> instance.
/// </summary>
public interface IContentFieldSetIndexHandler
{
    Task BuildIndexAsync(ContentFieldSet contentFieldSet, ContentTypeFieldSetDefinition typeFieldSetDefinition, BuildDocumentIndexContext context, IContentIndexSettings settings);
}
