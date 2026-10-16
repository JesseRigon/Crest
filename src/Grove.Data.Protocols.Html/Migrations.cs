using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Grove.Contents.Management;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Settings;
using Grove.Contents.Management.Records;
using Grove.Data.Migration;
using Grove.Data.Protocols.Html.Models;
using Grove.Data.Protocols.Html.Settings;
using YesSql;
using Grove.Contents.Types;

namespace Grove.Data.Protocols.Html;

public sealed class Migrations : DataMigration
{
    private readonly ISession _session;
    private readonly ILogger _logger;
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(
        IContentDefinitionManager contentDefinitionManager,
        ISession session,
        ILogger<Migrations> logger)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _session = session;
        _logger = logger;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterFieldSetDefinitionAsync("HtmlBody", builder => builder
            .Attachable()
            .WithDescription("Provides an HTML Body for your content item."));

        await DefineHtmlBodyFieldsAsync();

        // Shortcut other migration steps on new content definition schemas.
        return 7;
    }

    // This code can be removed in a later version.
    public static int UpdateFrom1()
    {
        return 2;
    }

    // This code can be removed in a later version.
    public static int UpdateFrom2()
    {
        return 3;
    }

    // This code can be removed in a later version.
    public async Task<int> UpdateFrom3Async()
    {
        // Update content type definitions
        foreach (var contentType in await _contentDefinitionManager.LoadTypeDefinitionsAsync())
        {
            if (contentType.FieldSets.Any(x => x.FieldSetDefinition.Name == "BodyPart"))
            {
                await _contentDefinitionManager.AlterTypeDefinitionAsync(contentType.Name, x => x.RemoveFieldSet("BodyPart").WithFieldSet("HtmlBody"));
            }
        }

        await _contentDefinitionManager.DeleteFieldSetDefinitionAsync("BodyPart");

        // We are patching all content item versions by moving the Title to DisplayText
        // This step doesn't need to be executed for a brand new site

        var lastDocumentId = 0L;

        for (; ; )
        {
            var contentItemVersions = await _session.Query<ContentItem, ContentItemIndex>(x => x.DocumentId > lastDocumentId).Take(10).ListAsync();

            if (!contentItemVersions.Any())
            {
                // No more content item version to process
                break;
            }

            foreach (var contentItemVersion in contentItemVersions)
            {
                if (UpdateBody((JsonObject)contentItemVersion.Content))
                {
                    await _session.SaveAsync(contentItemVersion);
                    if (_logger.IsEnabled(LogLevel.Information))
                    {
                        _logger.LogInformation("A content item version's BodyPart was upgraded: {ContentItemVersionId}", contentItemVersion.ContentItemVersionId);
                    }
                }

                lastDocumentId = contentItemVersion.Id;
            }

            await _session.FlushAsync();
        }

        static bool UpdateBody(JsonNode content)
        {
            var changed = false;

            if (content.GetValueKind() == JsonValueKind.Object)
            {
                var body = content["BodyPart"]?["Body"]?.Value<string>();

                if (!string.IsNullOrWhiteSpace(body))
                {
                    content["HtmlBody"] = new JsonObject() { ["Html"] = body };
                    changed = true;
                }

                foreach (var node in content.AsObject())
                {
                    changed = UpdateBody(node.Value) || changed;
                }
            }

            if (content.GetValueKind() == JsonValueKind.Array)
            {
                foreach (var node in content.AsArray())
                {
                    changed = UpdateBody(node) || changed;
                }
            }

            return changed;
        }

        return 5; // Returning 5 instead of 4, because UpdateFrom5 is no longer needed, see below why.
    }

    // Previously, Liquid rendering was enabled by not having Html sanitization enabled and UpdateFrom5Async disabled
    // sanitization to ensure that HtmlBodyParts kept Liquid rendering enabled. Since Liquid rendering is now controlled
    // by a separate setting, disabling sanitization is no longer necessary.

    public async Task<int> UpdateFrom5Async()
    {
        // To keep the same behavior as before, RenderLiquid is initialized to the opposite of SanitizeHtml.
        foreach (var contentType in await _contentDefinitionManager.LoadTypeDefinitionsAsync())
        {
            if (contentType.FieldSets.Any(p => p.FieldSetDefinition.Name == "HtmlBody"))
            {
                await _contentDefinitionManager.AlterTypeDefinitionAsync(contentType.Name, t => t.WithFieldSet("HtmlBody", part =>
                {
                    part.MergeSettings<HtmlBodyFieldSetSettings>(s => s.RenderLiquid = !s.SanitizeHtml);
                }));
            }
        }

        return 6;
    }

    // The field set's values are fields (docs/content-engine.md › Behaviour is workflows); the ones its
    // system flows require are locked by the module.
    public async Task<int> UpdateFrom6Async()
    {
        await DefineHtmlBodyFieldsAsync();
        return 7;
    }

    private Task DefineHtmlBodyFieldsAsync() =>
        _contentDefinitionManager.AlterFieldSetDefinitionAsync("HtmlBody", builder => builder
            .WithField(HtmlBodyFieldSet.HtmlField, field => field
                .OfType("HtmlField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Body")
                .WithPosition("0")
                .Locked()));
}
