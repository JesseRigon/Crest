using System.Text.Json.Nodes;
using Crest.Contents.AuditTrail.Services;

namespace Crest.Tests.Modules.Crest.Contents.AuditTrail;

/// <summary>Content history is stored as diffs with checkpoints (docs/audit.md): replaying must give every version back.</summary>
public class JsonTreeDiffTests
{
    private static JsonNode Node(string json) => JsonNode.Parse(json)!;

    [Fact]
    public void ApplyingTheDiffGivesTheNewVersion()
    {
        var before = Node("""{"ContentItemId":"a","DisplayText":"One","TitlePart":{"Title":"One"},"Tags":["x"],"Gone":1}""");
        var after = Node("""{"ContentItemId":"a","DisplayText":"Two","TitlePart":{"Title":"Two","Extra":true},"Tags":["x","y"],"New":{"Deep":{"Value":3}}}""");

        var diff = JsonTreeDiff.Diff(before, after);

        Assert.True(JsonNode.DeepEquals(after, JsonTreeDiff.Apply(before, diff)));
    }

    [Fact]
    public void UnchangedVersionsDiffToNothing()
    {
        var item = Node("""{"A":{"B":[1,2,{"C":null}]}}""");

        Assert.Empty(JsonTreeDiff.Diff(item, item.DeepClone()));
    }

    [Fact]
    public void ADiffStoresOnlyWhatChanged()
    {
        var before = Node("""{"Big":{"Body":"unchanged text"},"DisplayText":"One"}""");
        var after = Node("""{"Big":{"Body":"unchanged text"},"DisplayText":"Two"}""");

        var diff = JsonTreeDiff.Diff(before, after);

        var operation = Assert.Single(diff)!.AsObject();
        Assert.Equal("set", operation["op"]!.GetValue<string>());
        Assert.Equal("DisplayText", operation["path"]![0]!.GetValue<string>());
    }

    [Fact]
    public void ACheckpointAndItsDiffsRebuildEveryVersion()
    {
        var versions = Enumerable.Range(1, 12)
            .Select(index => Node($$$"""{"ContentItemId":"a","DisplayText":"v{{{index}}}","Part":{"Count":{{{index}}},"Flag":{{{(index % 2 == 0 ? "true" : "false")}}}}}"""))
            .ToArray();

        var checkpoint = JsonTreeDiff.Compress(versions[0]);
        var diffs = versions.Zip(versions.Skip(1), JsonTreeDiff.Diff).ToArray();

        var state = JsonTreeDiff.Decompress(checkpoint);
        Assert.True(JsonNode.DeepEquals(versions[0], state));
        for (var index = 0; index < diffs.Length; index++)
        {
            state = JsonTreeDiff.Apply(state, diffs[index]);
            Assert.True(JsonNode.DeepEquals(versions[index + 1], state));
        }
    }

    [Fact]
    public void ApplyLeavesTheBaseUntouched()
    {
        var before = Node("""{"A":1}""");
        var diff = JsonTreeDiff.Diff(before, Node("""{"A":2}"""));

        JsonTreeDiff.Apply(before, diff);

        Assert.Equal(1, before["A"]!.GetValue<int>());
    }
}
