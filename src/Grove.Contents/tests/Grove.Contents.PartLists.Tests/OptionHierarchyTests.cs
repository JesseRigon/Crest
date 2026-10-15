using Grove.Models;
using Grove.Services;
using Xunit;

namespace Grove.Contents.PartLists.Tests;

public class OptionHierarchyTests
{
    private static GroveOptionModel Option(string key, string? parent = null) =>
        new(key, key, key.ToUpperInvariant(), OptionSources.Module, false, 0, OptionCategories.Uncategorized, Parent: parent);

    private static readonly GroveOptionModel[] Naics =
    [
        Option("31-33"),
        Option("311", "31-33"),
        Option("3118", "311"),
        Option("31181", "3118"),
        Option("42"),
    ];

    [Fact]
    public void ValidateParent_AcceptsTheTopAndAnExistingOption()
    {
        Assert.Null(ContentPartListRules.ValidateParent(Naics, "new", null));
        Assert.Null(ContentPartListRules.ValidateParent(Naics, "new", "  "));
        Assert.Null(ContentPartListRules.ValidateParent(Naics, "new", "3118"));
    }

    [Fact]
    public void ValidateParent_RefusesAStrangerAndACycle()
    {
        Assert.NotNull(ContentPartListRules.ValidateParent(Naics, "new", "999"));
        // Moving the sector under its own grandchild would make it its own ancestor.
        Assert.NotNull(ContentPartListRules.ValidateParent(Naics, "31-33", "3118"));
        Assert.NotNull(ContentPartListRules.ValidateParent(Naics, "311", "311"));
    }

    [Fact]
    public void Children_BrowseOneLevel_WithWhetherAnythingSitsBelow()
    {
        var top = ContentPartListRules.Children(Naics, null).ToArray();
        Assert.Equal(["31-33", "42"], top.Select(node => node.Option.Key));
        Assert.True(top[0].HasChildren);
        Assert.False(top[1].HasChildren);

        Assert.Equal(["3118"], ContentPartListRules.Children(Naics, "311").Select(node => node.Option.Key));
    }

    [Fact]
    public void Ancestors_AreTopFirst()
    {
        var byKey = Naics.ToDictionary(option => option.Key, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(["31-33", "311", "3118"], ContentPartListRules.Ancestors(byKey, byKey["31181"]).Select(option => option.Key));
        Assert.Empty(ContentPartListRules.Ancestors(byKey, byKey["42"]));
    }
}
