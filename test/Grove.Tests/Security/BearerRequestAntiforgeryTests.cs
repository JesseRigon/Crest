using Grove.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;

namespace Grove.Tests.Security;

// A bearer-token request needs no antiforgery token; every other request is validated as before
// (docs/machine-actors.md).
public sealed class BearerRequestAntiforgeryTests
{
    [Theory]
    [InlineData("Bearer abc", true)]
    [InlineData("bearer abc", true)]
    [InlineData("Bearer ", false)]
    [InlineData("Basic dXNlcjpwYXNz", false)]
    [InlineData(null, false)]
    public async Task OnlyABearerRequestSkipsValidation(string? authorization, bool skipped)
    {
        var inner = new Mock<IAntiforgery>();
        inner.Setup(antiforgery => antiforgery.IsRequestValidAsync(It.IsAny<HttpContext>())).ReturnsAsync(false);
        var context = new DefaultHttpContext();
        if (authorization is not null)
        {
            context.Request.Headers.Authorization = authorization;
        }

        var valid = await new BearerRequestAntiforgery(inner.Object).IsRequestValidAsync(context);

        Assert.Equal(skipped, valid);
        inner.Verify(antiforgery => antiforgery.IsRequestValidAsync(context), skipped ? Times.Never() : Times.Once());
    }
}
