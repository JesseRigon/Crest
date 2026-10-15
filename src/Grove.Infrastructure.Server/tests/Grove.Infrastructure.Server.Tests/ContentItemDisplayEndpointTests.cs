using Grove.Access;
using Grove.Contents.Management;
using Grove.Contents.Management.Display;
using Microsoft.AspNetCore.Http.HttpResults;
using Grove.Display.Management;
using Grove.Display.Management.Components;
using Grove.Display.Management.Shapes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Grove.Infrastructure.Server.Tests;

// The headless display endpoint's access check (docs/blazor-display.md § 8, § 10): ViewContent
// on the item, asked of the one decision for the request's caller, before anything is built.
public sealed class ContentItemDisplayEndpointTests
{
    private const string ItemId = "item-1";

    private readonly IContentManager _contentManager = Substitute.For<IContentManager>();
    private readonly ICallerContextAccessor _callers = Substitute.For<ICallerContextAccessor>();
    private readonly IAccessDecision _decision = Substitute.For<IAccessDecision>();
    private readonly IContentItemDisplayManager _displayManager = Substitute.For<IContentItemDisplayManager>();
    private readonly IDisplayTreeBuilder _trees = Substitute.For<IDisplayTreeBuilder>();
    private readonly ContentItem _item = new() { ContentItemId = ItemId, ContentType = "Article", Published = true };
    private readonly CallerContext _caller = new() { Tenant = "Default", Side = CallerSide.Admin, UserId = "u1" };

    public ContentItemDisplayEndpointTests()
    {
        _callers.Current.Returns(_caller);
        _contentManager.GetAsync(ItemId, VersionOptions.Published).Returns(_item);
        _displayManager.BuildDisplayAsync(Arg.Any<ContentItem>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(new Shape());
        _trees.BuildAsync(Arg.Any<IShape>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new DisplayTree { Root = DisplayNode.Markup("root", string.Empty), DisplayType = "Detail" });
    }

    [Fact]
    public async Task ReturnsTheTreeWhenTheCallerMayViewTheItem()
    {
        Decide("ViewContent", AccessDecision.Allowed);

        var result = await DisplayAsync();

        var tree = Assert.IsType<DisplayTree>(Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal("root", tree.Root.Id);
        await _displayManager.Received(1).BuildDisplayAsync(_item, "Detail", Arg.Any<string>());
    }

    [Fact]
    public async Task ForbidsAndBuildsNothingWhenTheDecisionDenies()
    {
        Decide("ViewContent", AccessDecision.Denied("no"));

        var result = await DisplayAsync();

        Assert.IsType<ForbidHttpResult>(result);
        await _displayManager.DidNotReceiveWithAnyArgs().BuildDisplayAsync(default!, default!, default!);
        await _trees.DidNotReceiveWithAnyArgs().BuildAsync(default(IShape)!, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HidesTheItemWhenTheDecisionSaysNotFound()
    {
        Decide("ViewContent", new AccessDecision(AccessVerdict.NotFound));

        Assert.IsType<NotFound>(await DisplayAsync());
    }

    [Fact]
    public async Task ForbidsWhenNoCallerIsSet()
    {
        _callers.Current.Returns((CallerContext?)null);

        Assert.IsType<ForbidHttpResult>(await DisplayAsync());
        await _decision.DidNotReceiveWithAnyArgs().DecideAsync(default!, default!, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ReturnsNotFoundForAMissingItem()
    {
        Assert.IsType<NotFound>(await DisplayAsync("missing"));
    }

    [Fact]
    public async Task ShowsAnUnpublishedDraftOnlyToAnEditor()
    {
        var draft = new ContentItem { ContentItemId = "draft", ContentType = "Article" };
        _contentManager.GetAsync("draft", VersionOptions.Latest).Returns(draft);
        Decide("ViewContent", AccessDecision.Allowed);
        Decide("EditContent", AccessDecision.Denied("no"));

        Assert.IsType<NotFound>(await DisplayAsync("draft"));

        Decide("EditContent", AccessDecision.Allowed);

        Assert.IsAssignableFrom<IValueHttpResult>(await DisplayAsync("draft"));
    }

    [Fact]
    public async Task RefusesAMalformedDisplayType()
    {
        Decide("ViewContent", AccessDecision.Allowed);

        Assert.IsType<BadRequest>(await DisplayAsync(displayType: "Detail__<x>"));
    }

    private void Decide(string permission, AccessDecision verdict) =>
        _decision.DecideAsync(_caller, permission, Arg.Any<object?>(), Arg.Any<CancellationToken>()).Returns(verdict);

    private Task<IResult> DisplayAsync(string contentItemId = ItemId, string? displayType = null)
    {
        // Only the collaborators the display action uses; the others are not touched by it.
        var handlers = new Grove.Contents.Api.ContentItemsHandlers(null!, null!, _contentManager, null!, null!, null!, _callers, null!);

        // A published item's tree is versioned (decision 12), which reads the version service.
        handlers.BindTo(new DefaultHttpContext { RequestServices = new ServiceCollection().AddSingleton<DisplayTreeVersions>().BuildServiceProvider() });

        // The node filter passes the tree through: its own tests cover what it leaves out.
        var filter = Substitute.For<IDisplayTreeFilter>();
        filter.FilterAsync(Arg.Any<DisplayNode>(), Arg.Any<Grove.Access.CallerContext?>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<DisplayNode>());

        return handlers.DisplayAsync(contentItemId, _decision, _displayManager, _trees, filter, Substitute.For<IDisplayHtmlRenderer>(), displayType);
    }
}
