using System.Net;
using System.Security.Cryptography;
using System.Text;
using Crest.Workflows.Connectors;
using Xunit;

namespace Crest.Workflows.Tests;

// The connector surfaces a tenant controls: where a call may go (the address policy and
// the base-URL containment of a path) and what an anonymous webhook must prove.
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
        Assert.False(ConnectorNetworkPolicy.IsPublic(IPAddress.Parse(address)));

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("172.32.0.1")]
    [InlineData("93.184.216.34")]
    [InlineData("2606:4700:4700::1111")]
    public void Public_addresses_are_allowed(string address) =>
        Assert.True(ConnectorNetworkPolicy.IsPublic(IPAddress.Parse(address)));

    [Theory]
    [InlineData("https://api.example.com/v1", "invoices", "https://api.example.com/v1/invoices")]
    [InlineData("https://api.example.com/v1/", "/invoices?page=2", "https://api.example.com/v1/invoices?page=2")]
    [InlineData("https://api.example.com", "", "https://api.example.com/")]
    public void Paths_resolve_under_the_base_url(string baseUrl, string path, string expected)
    {
        Assert.True(ConnectorInvoker.TryResolve(new WorkflowConnection { Key = "c", BaseUrl = baseUrl }, path, out var uri, out _));
        Assert.Equal(expected, uri.AbsoluteUri);
    }

    [Theory]
    [InlineData("https://evil.example.com/x")]
    [InlineData("//evil.example.com/x")]
    [InlineData("../admin")]
    [InlineData("v2/../../admin")]
    [InlineData("\\\\evil\\x")]
    public void Paths_cannot_leave_the_base_url(string path) =>
        Assert.False(ConnectorInvoker.TryResolve(new WorkflowConnection { Key = "c", BaseUrl = "https://api.example.com/v1/" }, path, out _, out _));

    [Fact]
    public void Webhook_signature_must_match_the_body_and_secret()
    {
        var body = Encoding.UTF8.GetBytes("{\"paid\":true}");
        var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes("secret"), body)).ToLowerInvariant();

        Assert.True(WorkflowWebhooksController.IsValidSignature(signature, body, "secret"));
        Assert.True(WorkflowWebhooksController.IsValidSignature($"sha256={signature}", body, "secret"));
        Assert.False(WorkflowWebhooksController.IsValidSignature(signature, body, "other"));
        Assert.False(WorkflowWebhooksController.IsValidSignature(signature, Encoding.UTF8.GetBytes("{\"paid\":false}"), "secret"));
        Assert.False(WorkflowWebhooksController.IsValidSignature("", body, "secret"));
        Assert.False(WorkflowWebhooksController.IsValidSignature("not-hex", body, "secret"));
    }
}
