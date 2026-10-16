using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Seo.Models;

namespace Grove.Seo.Services;

/// <summary>What the SEO's system flow does (<see cref="Workflows.SeoMetaWorkflowProvider"/>): the canonical address holds no characters an address may not hold, and the Google schema is JSON.</summary>
public sealed class SeoMetaBehaviour
{
    public Task<ContentFieldRefusal> RefusalAsync(ContentItem item)
    {
        if (!item.TryGet<SeoMetaFieldSet>(out var part))
        {
            return Task.FromResult<ContentFieldRefusal>(null);
        }

        var fieldSet = ContentFieldSetName.Of<SeoMetaFieldSet>();
        if (part.Canonical?.IndexOfAny(SeoMetaFieldSet.InvalidCharactersForCanoncial) > -1 || part.Canonical?.IndexOf(' ') > -1)
        {
            return Task.FromResult(new ContentFieldRefusal("The canonical entry contains invalid characters.", $"{fieldSet}.{nameof(SeoMetaFieldSet.Canonical)}"));
        }

        if (!string.IsNullOrWhiteSpace(part.GoogleSchema))
        {
            try
            {
                System.Text.Json.Nodes.JsonNode.Parse(part.GoogleSchema);
            }
            catch (System.Text.Json.JsonException)
            {
                return Task.FromResult(new ContentFieldRefusal("The google schema is written in an incorrect format.", $"{fieldSet}.{nameof(SeoMetaFieldSet.GoogleSchema)}"));
            }
        }

        return Task.FromResult<ContentFieldRefusal>(null);
    }
}
