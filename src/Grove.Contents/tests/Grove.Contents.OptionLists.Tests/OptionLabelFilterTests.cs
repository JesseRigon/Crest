using Grove.Contents.OptionLists.Liquid;
using Grove.Models;
using Xunit;

namespace Grove.Contents.OptionLists.Tests;

// option_label reads the label a human sees for what content stores (docs/content-engine.md › Liquid over content):
// a ContentItemId always, an option key only when the list is named.
public class OptionLabelFilterTests
{
    private static readonly GroveOptionModel[] Options =
    [
        new("4abc", "Open", "Open order", "Module", false, 0, "Uncategorized"),
        new("4def", "Closed", "Closed order", "Module", false, 1, "Uncategorized"),
    ];

    [Fact]
    public void Reads_the_label_of_a_stored_id() =>
        Assert.Equal("Closed order", OptionLabelFilter.Label(Options, "4DEF", byKey: false));

    [Fact]
    public void Reads_a_key_only_when_the_list_is_named()
    {
        Assert.Null(OptionLabelFilter.Label(Options, "Open", byKey: false));
        Assert.Equal("Open order", OptionLabelFilter.Label(Options, "open", byKey: true));
    }

    [Fact]
    public void An_unknown_value_has_no_label() =>
        Assert.Null(OptionLabelFilter.Label(Options, "4xyz", byKey: true));
}
