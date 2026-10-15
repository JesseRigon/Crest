using System.Text.Json.Nodes;
using Grove.Components.Content;
using Xunit;

namespace Grove.Components.Tests;

public sealed class ContentDiffTests
{
    [Fact]
    public void Changed_added_and_removed_values_are_listed_by_path_and_bookkeeping_is_left_out()
    {
        var before = JsonNode.Parse("""{ "ModifiedUtc": "2026-01-01", "TitlePart": { "Title": "Old" }, "Tags": ["a", "b"], "Gone": 1 }""");
        var after = JsonNode.Parse("""{ "ModifiedUtc": "2026-02-01", "TitlePart": { "Title": "New" }, "Tags": ["a", "b", "c"] }""");

        var changes = ContentDiff.Changes(before, after);

        Assert.Contains(changes, change => change == ("TitlePart.Title", "Old", "New"));
        Assert.Contains(changes, change => change == ("Tags[2]", null, "c"));
        Assert.Contains(changes, change => change == ("Gone", "1", null));
        Assert.DoesNotContain(changes, change => change.Path == "ModifiedUtc");
        Assert.DoesNotContain(changes, change => change.Path == "Tags[0]");
    }

    [Fact]
    public void Identical_versions_have_no_changes() =>
        Assert.Empty(ContentDiff.Changes(JsonNode.Parse("""{ "A": 1 }"""), JsonNode.Parse("""{ "A": 1 }""")));
}
