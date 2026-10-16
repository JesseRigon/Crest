using System.Net;
using System.Security.Cryptography;
using System.Text;
using Grove.Data.Protocols.Http;
using Grove.Data.Protocols.OAuth;
using Grove.Data.Protocols.Webhooks;
using Xunit;

namespace Grove.Data.Connectors.Tests;

// The surfaces a tenant controls: where a call may go (the address guard and the base-URL
// containment of a path) and what an anonymous webhook must prove.
public class ConnectorSecurityTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")]
    [InlineData("100.64.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    [InlineData("fd00::1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("::ffff:10.0.0.1")]
    public void Non_public_addresses_are_refused(string address) =>
        Assert.False(NetworkGuard.IsPublic(IPAddress.Parse(address)));

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("172.32.0.1")]
    [InlineData("93.184.216.34")]
    [InlineData("2606:4700:4700::1111")]
    public void Public_addresses_are_allowed(string address) =>
        Assert.True(NetworkGuard.IsPublic(IPAddress.Parse(address)));

    [Fact]
    public async Task A_literal_private_host_is_refused_before_connecting()
    {
        await Assert.ThrowsAsync<ConnectorRefusedException>(() => NetworkGuard.ResolveAsync("10.0.0.5", allowPrivate: false));
        Assert.NotEmpty(await NetworkGuard.ResolveAsync("10.0.0.5", allowPrivate: true));
    }

    [Theory]
    [InlineData("https://api.example.com/v1", "invoices", "https://api.example.com/v1/invoices")]
    [InlineData("https://api.example.com/v1/", "/invoices?page=2", "https://api.example.com/v1/invoices?page=2")]
    [InlineData("https://api.example.com", "", "https://api.example.com/")]
    public void Paths_resolve_under_the_base_url(string baseUrl, string path, string expected)
    {
        Assert.True(BaseUrlPath.TryResolve(baseUrl, path, out var uri, out _));
        Assert.Equal(expected, uri.AbsoluteUri);
    }

    [Theory]
    [InlineData("https://evil.example.com/x")]
    [InlineData("//evil.example.com/x")]
    [InlineData("../admin")]
    [InlineData("v2/../../admin")]
    [InlineData("\\\\evil\\x")]
    public void Paths_cannot_leave_the_base_url(string path) =>
        Assert.False(BaseUrlPath.TryResolve("https://api.example.com/v1/", path, out _, out _));

    [Fact]
    public void A_next_page_link_elsewhere_is_dropped() =>
        Assert.Null(BaseUrlPath.Relativize("https://api.example.com/v1/", "https://evil.example.com/v1/items?page=2"));

    [Fact]
    public void Webhook_signature_must_match_the_body_and_secret()
    {
        var scheme = new HmacSha256HexScheme();
        var body = Encoding.UTF8.GetBytes("{\"paid\":true}");
        var secret = Encoding.UTF8.GetBytes("secret");
        var signature = Convert.ToHexString(HMACSHA256.HashData(secret, body)).ToLowerInvariant();

        static WebhookRequest Request(byte[] body, string signature) => new(body, new Dictionary<string, string> { ["X-Signature"] = signature });

        Assert.True(scheme.Verify(secret, Request(body, signature), null));
        Assert.True(scheme.Verify(secret, Request(body, $"sha256={signature}"), null));
        Assert.False(scheme.Verify(Encoding.UTF8.GetBytes("other"), Request(body, signature), null));
        Assert.False(scheme.Verify(secret, Request(Encoding.UTF8.GetBytes("{\"paid\":false}"), signature), null));
        Assert.False(scheme.Verify(secret, Request(body, ""), null));
        Assert.False(scheme.Verify(secret, Request(body, "not-hex"), null));
        Assert.False(scheme.Verify(secret, Request(body, signature), "X-Other-Header"));
    }

    [Fact]
    public void Pkce_challenge_is_the_base64url_sha256_of_the_verifier() =>
        Assert.Equal("_1J8RhqeWMlcSiJAE1UQ5O7bXSZjdgTomMAUuXIusRU", OAuthClient.CodeChallenge("grove-pkce-verifier-0123456789-abcdefghijklmnop"));

    [Fact]
    public void Authorize_url_carries_state_and_challenge()
    {
        var connection = new ConnectionBuilder("crm").OAuth2AuthorizationCode("https://auth.example.com/authorize", "https://auth.example.com/token", ["read"]).Build();
        var url = new OAuthClient().BuildAuthorizeUrl(connection, new AppCredentials("client", "secret"), "https://grove.example.com/cb", "sealed", "verifier");
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(url.Query);
        Assert.Equal("sealed", query["state"].ToString());
        Assert.Equal("S256", query["code_challenge_method"].ToString());
        Assert.Equal(OAuthClient.CodeChallenge("verifier"), query["code_challenge"].ToString());
        Assert.Equal("read", query["scope"].ToString());
        Assert.False(query.ContainsKey("client_secret"));
    }
}
