using System.Text.Json.Nodes;
using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Fields;
using Grove.Infrastructure.Settings;
using Grove.Models;

namespace Grove.Services;

/// <summary>
/// A reference in a snapshot keeps both (docs/content-engine.md › Live and snapshot embeddings):
/// when a snapshot embedding's item freezes, each option list field writes beside its ids the
/// referenced values the embedding names (<c>Copied</c>, by id), read through the field's source.
/// The option's key is always kept.
/// </summary>
public sealed class OptionListFieldSnapshotHandler(IServiceProvider services) : IEmbeddedContentSnapshotHandler
{
    // Resolved when used: sources reach the content manager, which runs this handler.
    private IEnumerable<IOptionSourceProvider> Providers =>
        Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetServices<IOptionSourceProvider>(services);

    public async Task FreezeAsync(EmbeddedContentSnapshotContext context, CancellationToken cancellationToken = default)
    {
        JsonObject content = context.ContentItem.Content;
        foreach (var typeFieldSet in context.ContentType.FieldSets)
        {
            foreach (var field in typeFieldSet.FieldSetDefinition.Fields.Where(field => field.FieldTypeDefinition.Name == nameof(OptionListField)))
            {
                if (content[typeFieldSet.Name]?[field.Name] is not JsonObject stored
                    || stored["SelectedIds"] is not JsonArray selected || selected.Count == 0)
                {
                    continue;
                }

                var sourceKey = stored["SourceKey"]?.ToString() is { Length: > 0 } key ? key : field.GetSettings<OptionListFieldSettings>().SourceKey;
                var (providerKey, qualifier) = OptionSourceKeys.Split(sourceKey ?? string.Empty);
                var provider = Providers.FirstOrDefault(candidate => string.Equals(candidate.Key, providerKey, StringComparison.OrdinalIgnoreCase));
                if (provider is null)
                {
                    continue;
                }

                var ids = selected.Select(id => id?.ToString()).Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
                var rows = await provider.GetByIdsAsync(qualifier, ids, [.. context.Embedding.CopiedValues], cancellationToken);
                var copied = new JsonObject();
                foreach (var row in rows)
                {
                    var values = new JsonObject { ["Key"] = row.Key ?? row.Id };
                    foreach (var column in context.Embedding.CopiedValues)
                    {
                        values[column] = row.Values.TryGetValue(column, out var value) ? value : null;
                    }

                    copied[row.Id] = values;
                }

                stored["Copied"] = copied;
            }
        }
    }
}
