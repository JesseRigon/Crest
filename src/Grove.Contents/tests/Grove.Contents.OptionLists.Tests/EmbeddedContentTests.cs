using System.Text.Json.Dynamic;
using System.Text.Json.Nodes;
using Grove.Contents.Embedding;
using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Builders;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents.Management.Metadata.Records;
using Grove.Contents.Management.Metadata.Settings;
using Grove.Contents.Types.Api;
using Microsoft.Extensions.Localization;
using NSubstitute;
using Xunit;

namespace Grove.Contents.OptionLists.Tests;

// Embedded content items (docs/content-engine.md › Embedded content items): the declaration on the
// type, stable ids, validation, nesting and snapshot embeddings.
public class EmbeddedContentTests
{
    private static ContentTypeDefinition Line() => new ContentTypeDefinitionBuilder()
        .WithName("Line")
        .WithDisplayName("Line")
        .WithFieldSet("Line", part => { })
        .Build();

    private static ContentTypeDefinition Invoice(Action<EmbeddedContentDefinitionBuilder>? lines = null) => new ContentTypeDefinitionBuilder()
        .WithName("Invoice")
        .WithDisplayName("Invoice")
        .WithFieldSet("Invoice", part => { })
        .WithEmbedding("Lines", embedding =>
        {
            embedding.WithDisplayName("Lines").WithPosition("2").Allow("Line").WithCount(1, 3);
            lines?.Invoke(embedding);
        })
        .Build();

    private static ContentItem Item(string type, JsonObject? content = null)
    {
        var item = new ContentItem { ContentItemId = type.ToLowerInvariant() + "-1", ContentType = type };
        foreach (var (name, value) in content ?? [])
        {
            item.Content[name] = value?.DeepClone();
        }

        return item;
    }

    private static JsonObject Lines(params JsonObject[] items) => new() { ["ContentItems"] = new JsonArray([.. items]) };

    private static JsonObject LineJson(string? id = null) => id is null
        ? new JsonObject { ["ContentType"] = "Line", ["DisplayText"] = "A line" }
        : new JsonObject { ["ContentItemId"] = id, ["ContentType"] = "Line", ["DisplayText"] = "A line" };

    private static JsonObject Json(ContentItem item) => (JsonObject)(JsonDynamicObject)item.Content;

    private static EmbeddedContentHandler Handler(ContentTypeDefinition parent, IEnumerable<IEmbeddedContentSnapshotHandler>? snapshots = null)
    {
        var definitions = Substitute.For<IContentDefinitionManager>();
        definitions.GetTypeDefinitionAsync(parent.Name).Returns(parent);
        definitions.GetTypeDefinitionAsync("Line").Returns(Line());

        var ids = Substitute.For<IContentItemIdGenerator>();
        var next = 0;
        ids.GenerateUniqueId(Arg.Any<ContentItem>()).Returns(_ => $"generated-{++next}");

        var localizer = Substitute.For<IStringLocalizer<EmbeddedContentHandler>>();
        localizer[Arg.Any<string>(), Arg.Any<object[]>()].Returns(call => new LocalizedString(call.Arg<string>()!, string.Format(call.Arg<string>()!, call.Arg<object[]>()!)));
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        foreach (var snapshot in snapshots ?? [])
        {
            Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton(services, snapshot);
        }

        return new EmbeddedContentHandler(definitions, ids, new EmbeddedContentReader(definitions), Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(services), localizer);
    }

    [Fact]
    public void An_embedding_is_declared_beside_the_field_sets_and_round_trips_through_its_record()
    {
        var invoice = Invoice(lines => lines.Snapshot("DisplayText").Referenceable().Collapsed());
        var embedding = Assert.Single(invoice.Embeddings);

        Assert.Equal(["Invoice"], invoice.FieldSets.Select(fieldSet => fieldSet.Name));
        Assert.Equal("Lines", embedding.Name);
        Assert.Equal(["Line"], embedding.AllowedTypes);
        Assert.Equal((1, 3), (embedding.MinCount, embedding.MaxCount));
        Assert.True(embedding.IsSnapshot);
        Assert.Equal(["DisplayText"], embedding.CopiedValues);

        var copy = new EmbeddedContentDefinition(embedding.ToRecord());
        Assert.Equal(embedding.ToRecord().AllowedTypes, copy.AllowedTypes);
        Assert.True(copy.Referenceable && copy.Collapsed && copy.IsSnapshot);
    }

    [Fact]
    public void An_embedding_never_takes_the_name_of_a_field_set_attachment() =>
        Assert.Throws<ArgumentException>(() => new ContentTypeDefinitionBuilder()
            .WithName("Invoice")
            .WithFieldSet("Lines", part => { })
            .WithEmbedding("Lines", embedding => embedding.Allow("Line"))
            .Build());

    [Fact]
    public void Allowed_types_are_names_stereotypes_or_any()
    {
        var widget = new ContentTypeDefinitionBuilder().WithName("Hero").Stereotype("Widget").Build();
        EmbeddedContentDefinition Allowing(params string[] types) => new(new EmbeddedContentDefinitionRecord { Name = "Blocks", AllowedTypes = types });

        Assert.True(Allowing("Hero").Allows(widget));
        Assert.True(Allowing("@Widget").Allows(widget));
        Assert.True(Allowing(EmbeddedContentDefinition.AnyType).Allows(widget));
        Assert.False(Allowing("Line").Allows(widget));
    }

    [Fact]
    public async Task Every_embedded_item_gets_a_stable_id_and_a_clone_gets_its_own()
    {
        var handler = Handler(Invoice());
        var invoice = Item("Invoice", new JsonObject { ["Lines"] = Lines(LineJson("kept"), LineJson()) });

        await handler.UpdatingAsync(new UpdateContentContext(invoice));
        var ids = Json(invoice)["Lines"]!["ContentItems"]!.AsArray().Select(line => line!["ContentItemId"]!.ToString()).ToArray();
        Assert.Equal("kept", ids[0]);
        Assert.Equal("generated-1", ids[1]);

        var clone = Item("Invoice", new JsonObject { ["Lines"] = Lines(LineJson("kept")) });
        await handler.CloningAsync(new CloneContentContext(invoice, clone));
        Assert.NotEqual("kept", Json(clone)["Lines"]!["ContentItems"]![0]!["ContentItemId"]!.ToString());
    }

    [Fact]
    public async Task Validation_refuses_counts_outside_the_limits_and_types_not_allowed_by_the_embedding_name()
    {
        var handler = Handler(Invoice());

        var empty = new ValidateContentContext(Item("Invoice"));
        await handler.ValidatingAsync(empty);
        Assert.Equal(["Lines"], Assert.Single(empty.ContentValidateResult.Errors).MemberNames);

        var foreign = new ValidateContentContext(Item("Invoice", new JsonObject
        {
            ["Lines"] = Lines(LineJson("a"), new JsonObject { ["ContentItemId"] = "b", ["ContentType"] = "Hero" }),
        }));
        await handler.ValidatingAsync(foreign);
        Assert.Contains(foreign.ContentValidateResult.Errors, error => error.ErrorMessage!.Contains("Hero"));

        var fine = new ValidateContentContext(Item("Invoice", new JsonObject { ["Lines"] = Lines(LineJson("a")) }));
        await handler.ValidatingAsync(fine);
        Assert.True(fine.ContentValidateResult.Succeeded);
    }

    [Fact]
    public async Task A_nested_embedding_reaches_every_depth()
    {
        var tree = new ContentTypeDefinitionBuilder()
            .WithName("Invoice")
            .WithEmbedding("Lines", embedding => embedding.Allow("Line").Nested())
            .Build();
        var child = LineJson();
        var parent = LineJson("root");
        parent["Lines"] = Lines(child);
        var invoice = Item("Invoice", new JsonObject { ["Lines"] = Lines(parent) });

        await Handler(tree).UpdatingAsync(new UpdateContentContext(invoice));

        var nested = Json(invoice)["Lines"]!["ContentItems"]![0]!["Lines"]!["ContentItems"]![0]!;
        Assert.Equal("generated-1", nested["ContentItemId"]!.ToString());
        Assert.Equal(["root", "generated-1"], tree.Embeddings.Single().AllItems(invoice).Select(item => item.ContentItemId));
    }

    [Fact]
    public async Task Publishing_freezes_a_snapshot_embedding_with_its_schema_and_lets_the_snapshot_handlers_write()
    {
        var snapshot = Substitute.For<IEmbeddedContentSnapshotHandler>();
        var invoice = Item("Invoice", new JsonObject { ["Lines"] = Lines(LineJson("a")) });

        await Handler(Invoice(lines => lines.Snapshot("DisplayText")), [snapshot]).PublishingAsync(new PublishContentContext(invoice, null));

        var schema = Json(invoice)["Lines"]!["Schema"]!;
        Assert.NotNull(schema[EmbeddedContentSchema.TypesKey]!["Line"]);
        Assert.True(EmbeddedContentSchema.IsFrozen(schema.AsObject(), "a"));
        await snapshot.Received(1).FreezeAsync(Arg.Is<EmbeddedContentSnapshotContext>(context => context != null && context.ContentItem.ContentItemId == "a" && context.ContentType.Name == "Line"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_frozen_item_reads_its_type_from_the_stored_schema()
    {
        var line = new ContentTypeDefinitionBuilder()
            .WithName("Line")
            .WithDisplayName("Line as published")
            .WithFieldSet("Line", part => part.WithDisplayName("Values"))
            .Build();
        var entry = EmbeddedContentSchema.Write(line);
        var read = EmbeddedContentSchema.Read("Line", entry);

        Assert.Equal("Line as published", read.DisplayName);
        Assert.Equal(["Line"], read.FieldSets.Select(fieldSet => fieldSet.Name));

        // The reader pairs a frozen item with its stored schema and a later item with the current definition.
        var current = Line();
        var definitions = Substitute.For<IContentDefinitionManager>();
        definitions.GetTypeDefinitionAsync("Invoice").Returns(Invoice(lines => lines.Snapshot()));
        definitions.GetTypeDefinitionAsync("Line").Returns(current);
        var lines = Lines(LineJson("frozen"), LineJson("later"));
        lines["Schema"] = new JsonObject
        {
            [EmbeddedContentSchema.TypesKey] = new JsonObject { ["Line"] = entry },
            [EmbeddedContentSchema.ItemsKey] = new JsonArray("frozen"),
        };
        var invoice = Item("Invoice", new JsonObject { ["Lines"] = lines });

        var entries = await new EmbeddedContentReader(definitions).ReadAsync(invoice, "Lines");

        Assert.Equal("Line as published", entries.Single(e => e.Item.ContentItemId == "frozen").Definition.DisplayName);
        Assert.True(entries.Single(e => e.Item.ContentItemId == "frozen").Frozen);
        Assert.Same(current, entries.Single(e => e.Item.ContentItemId == "later").Definition);

    }

    [Fact]
    public void The_type_editor_refuses_unknown_types_name_clashes_and_embedding_back_into_the_type()
    {
        var invoice = Invoice();
        var looping = new ContentTypeDefinitionBuilder().WithName("Line").WithEmbedding("Back", embedding => embedding.Allow("Invoice")).Build();

        Assert.NotEmpty(ContentTypesHandlers.EmbeddingProblems(invoice, new EmbeddingRequest("Invoice", AllowedTypes: ["Line"]), [invoice, Line()]));
        Assert.NotEmpty(ContentTypesHandlers.EmbeddingProblems(invoice, new EmbeddingRequest("Notes", AllowedTypes: ["Missing"]), [invoice, Line()]));
        Assert.NotEmpty(ContentTypesHandlers.EmbeddingProblems(invoice, new EmbeddingRequest("Notes", AllowedTypes: ["Line"]), [invoice, looping]));
        Assert.Empty(ContentTypesHandlers.EmbeddingProblems(invoice, new EmbeddingRequest("Notes", AllowedTypes: ["Line"]), [invoice, Line()]));
    }
}
