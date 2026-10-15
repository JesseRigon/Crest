using Microsoft.AspNetCore.Http;

namespace Grove.Tests.Abstractions;

// The server's own calls back into itself are exempt from request-level policies (rate limits),
// so the mark must be one only this process can produce.
public class LoopbackRequestsTests
{
    private static HttpContext Incoming(HttpRequestMessage outgoing)
    {
        var context = new DefaultHttpContext();
        foreach (var header in outgoing.Headers)
        {
            context.Request.Headers[header.Key] = header.Value.ToArray();
        }

        return context;
    }

    [Fact]
    public void AMarkedRequest_IsLoopback()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/api/grove/x");
        LoopbackRequests.Mark(request);

        Assert.True(LoopbackRequests.IsLoopback(Incoming(request)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("forged")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    public void AnUnmarkedOrForgedRequest_IsNotLoopback(string value)
    {
        var context = new DefaultHttpContext();
        if (value is not null)
        {
            context.Request.Headers[LoopbackRequests.HeaderName] = value;
        }

        Assert.False(LoopbackRequests.IsLoopback(context));
    }
}
