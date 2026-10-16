using Grove.Models;
using Grove.Services;
using NSubstitute;
using Xunit;

namespace Grove.Contents.OptionLists.Tests;

// The import upserts by key and never overwrites a tenant's label.
public sealed class OptionListImporterTests
{
    private static GroveOptionModel Option(string key, string label, int position) =>
        new($"id-{key}", key, label, OptionSources.Module, false, position, OptionCategories.Uncategorized);

    private static IOptionListService ListWith(params GroveOptionModel[] options)
    {
        var lists = Substitute.For<IOptionListService>();
        lists.GetAsync("units", Arg.Any<CancellationToken>())
            .Returns(new OptionListModel("list", "units", "Units", OptionSources.Module, "None", "None", options));
        return lists;
    }

    [Fact]
    public async Task ANewKeyIsAddedWithItsLabelAtTheEnd()
    {
        var lists = ListWith(Option("kg", "Kilogram", 3));

        var result = await new OptionListImporter(lists).ImportAsync("units", [new("g", "Gram", Category: "Mass")], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal((1, 1, 0, 0), (result.Read, result.Added, result.Updated, result.Skipped));
        await lists.Received(1).AddOptionAsync("units", "g", "Gram", 4, category: "Mass", value: null, fields: null, parent: null, cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnExistingKeyTakesTheSourcesDataButKeepsTheTenantsLabel()
    {
        var lists = ListWith(Option("kg", "Kilo (ours)", 0));

        var result = await new OptionListImporter(lists).ImportAsync("units", [new("kg", "Kilogram", Value: "1000")], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Updated);
        await lists.Received(1).UpdateOptionAsync("units", "kg", null, null, null, category: null, value: "1000", fields: null, parent: null, cancellationToken: Arg.Any<CancellationToken>());
        await lists.DidNotReceiveWithAnyArgs().AddOptionAsync(default!, default!, default!, cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task FilteredAndBlankRowsAreSkippedAndAFailingRowIsReported()
    {
        var lists = ListWith();
        lists.AddOptionAsync("units", "bad", Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<IReadOnlyDictionary<string, string?>?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns<GroveOptionModel>(_ => throw new InvalidOperationException("invalid key"));

        var result = await new OptionListImporter(lists).ImportAsync(
            "units",
            [new("", "Blank"), new("skip-me", "Skip"), new("bad", "Bad"), new("ok", "Ok")],
            include: row => row.Key != "skip-me",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal((4, 1, 0, 2), (result.Read, result.Added, result.Updated, result.Skipped));
        Assert.Equal(["bad: invalid key"], result.Errors);
    }
}
