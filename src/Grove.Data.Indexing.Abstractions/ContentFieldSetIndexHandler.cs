using Grove.Contents.Management;
using Grove.Contents.Management.Metadata.Models;

namespace Grove.Data.Indexing;

/// <summary>
/// An implementation of <see cref="ContentFieldSetIndexHandler&lt;TFieldSet&gt;"/> is able to take part in the rendering of
/// a <see typeparamref="TFieldSet"/> instance.
/// </summary>
public abstract class ContentFieldSetIndexHandler<TFieldSet> : IContentFieldSetIndexHandler where TFieldSet : ContentFieldSet
{
    Task IContentFieldSetIndexHandler.BuildIndexAsync(ContentFieldSet contentFieldSet, ContentTypeFieldSetDefinition typeFieldSetDefinition, BuildDocumentIndexContext context, IContentIndexSettings settings)
    {
        var part = contentFieldSet as TFieldSet;

        if (part == null)
        {
            return Task.CompletedTask;
        }

        var keys = new List<string>
        {
            typeFieldSetDefinition.Name,
        };

        foreach (var key in context.Keys)
        {
            keys.Add($"{key}.{typeFieldSetDefinition.Name}");
        }

        if (context.DocumentIndex is ContentItemDocumentIndex contentItemIndex && context.Record is ContentItem contentItem)
        {
            var buildFieldSetIndexContext = new BuildFieldSetIndexContext(contentItemIndex, contentItem, keys, typeFieldSetDefinition, settings);

            return BuildIndexAsync(part, buildFieldSetIndexContext);
        }

        return null;
    }

    public abstract Task BuildIndexAsync(TFieldSet part, BuildFieldSetIndexContext context);
}
