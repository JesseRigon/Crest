using Grove.Contents.Management;
using Grove.Contents.Management.Metadata.Models;

namespace Grove.Data.Indexing;

/// <summary>
/// An implementation of <see cref="ContentFieldIndexHandler&lt;TField&gt;"/> is able to take part in the rendering of
/// a <see typeparamref="TField"/> instance.
/// </summary>
public abstract class ContentFieldIndexHandler<TField> : IContentFieldIndexHandler where TField : ContentField
{
    Task IContentFieldIndexHandler.BuildIndexAsync(
        ContentFieldSet contentFieldSet,
        ContentTypeFieldSetDefinition typeFieldSetDefinition,
        ContentFieldDefinition partFieldDefinition,
        BuildDocumentIndexContext context,
        IContentIndexSettings settings)
    {
        if (contentFieldSet == null)
        {
            return Task.CompletedTask;
        }

        ArgumentNullException.ThrowIfNull(partFieldDefinition);

        if (!string.Equals(typeof(TField).Name, partFieldDefinition.FieldTypeDefinition.Name, StringComparison.Ordinal) &&
           !string.Equals(nameof(ContentField), partFieldDefinition.FieldTypeDefinition.Name, StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        var field = contentFieldSet.Get<TField>(partFieldDefinition.Name);
        if (field != null)
        {
            var keys = new List<string>();
            foreach (var key in context.Keys)
            {
                keys.Add($"{key}.{partFieldDefinition.Name}");
            }

            if (!keys.Contains($"{typeFieldSetDefinition.Name}.{partFieldDefinition.Name}"))
            {
                keys.Add($"{typeFieldSetDefinition.Name}.{partFieldDefinition.Name}");
            }

            if (context.DocumentIndex is ContentItemDocumentIndex contentItemIndex && context.Record is ContentItem contentItem)
            {
                var buildFieldIndexContext = new BuildFieldIndexContext(contentItemIndex, contentItem, keys, contentFieldSet, typeFieldSetDefinition, partFieldDefinition, settings);

                return BuildIndexAsync(field, buildFieldIndexContext);
            }

        }

        return Task.CompletedTask;
    }

    public abstract Task BuildIndexAsync(TField field, BuildFieldIndexContext context);
}
