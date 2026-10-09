using Microsoft.Playwright;
using Crest.Tests.Functional.Helpers;

namespace Crest.Tests.Functional.Tests.Mvc;

public sealed class MvcTests : IClassFixture<MvcSetupFixture>, IAsyncLifetime
{
    private readonly MvcSetupFixture _fixture;

    public MvcTests(MvcSetupFixture fixture)
    {
        _fixture = fixture;
    }

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        _fixture.AssertNoLoggedIssues();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Display_HelloWorld_Succeeds()
    {
        var page = await _fixture.CreatePageAsync();
        await page.GotoAndAssertOkAsync("/");
        await Assertions.Expect(page.Locator("body")).ToContainTextAsync("Hello World");
        await page.CloseAsync();
    }
}
