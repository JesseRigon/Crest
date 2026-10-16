using System.Text;
using System.Text.Json.Nodes;
using Grove.Communications.Delivery;
using Grove.Communications.Email;
using Grove.Data.Connectors;
using Grove.Integrations.Microsoft;
using Grove.Integrations.Microsoft.Connections;
using Grove.Integrations.Microsoft.GraphMail;
using Microsoft.Extensions.Logging.Abstractions;

namespace Grove.Communications.Tests;

public class GraphMailTests
{
    [Fact]
    public async Task Send_posts_send_mail_on_the_graph_connection_with_the_tenant_grant()
    {
        var engine = new FakeConnectorEngine { Respond = _ => new ConnectorResult { Outcome = ConnectorOutcome.Succeeded, StatusCode = 202 } };
        var provider = new GraphMailProvider(
            Monitors.Of(new GraphMailSettings { IsEnabled = true, SaveToSentItems = false }),
            engine,
            Callers.Factory(),
            NullLogger<GraphMailProvider>.Instance,
            new PassThroughLocalizer<GraphMailProvider>());

        var result = await provider.SendAsync(
            new MailMessage { To = "Ann <ann@example.com>; bob@example.com", Cc = "carl@example.com", Subject = "Hi", HtmlBody = "<p>Hello</p>" },
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(MicrosoftAuthenticationConstants.GraphConnectionKey, Assert.IsType<DeliveryResult>(result).ConnectionKey);
        var call = Assert.Single(engine.Calls);
        Assert.Equal(MicrosoftAuthenticationConstants.GraphConnectionKey, call.ConnectionKey);
        Assert.Equal(CredentialScope.Tenant, call.Scope);
        Assert.Equal(MicrosoftAuthenticationConstants.GraphOperations.SendMail, call.OperationId);
        Assert.Equal("/me/sendMail", call.Request.Path);

        var body = JsonNode.Parse(call.Request.Body)!;
        Assert.False(body["saveToSentItems"]!.GetValue<bool>());
        Assert.Equal("HTML", body["message"]!["body"]!["contentType"]!.GetValue<string>());
        var to = body["message"]!["toRecipients"]!.AsArray();
        Assert.Equal(2, to.Count);
        Assert.Equal("ann@example.com", to[0]!["emailAddress"]!["address"]!.GetValue<string>());
        Assert.Equal("Ann", to[0]!["emailAddress"]!["name"]!.GetValue<string>());
        Assert.Single(body["message"]!["ccRecipients"]!.AsArray());
    }

    [Fact]
    public async Task An_invalid_address_calls_nothing()
    {
        var engine = new FakeConnectorEngine();
        var provider = new GraphMailProvider(
            Monitors.Of(new GraphMailSettings { IsEnabled = true }),
            engine,
            Callers.Factory(),
            NullLogger<GraphMailProvider>.Instance,
            new PassThroughLocalizer<GraphMailProvider>());

        var result = await provider.SendAsync(new MailMessage { To = "not an address" }, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Empty(engine.Calls);
    }

    [Fact]
    public async Task The_mailbox_lists_after_the_cursor_then_reads_each_message()
    {
        const string mime = "Message-ID: <7@example.com>\r\nFrom: a@example.com\r\nTo: b@example.com\r\nSubject: Order\r\n\r\nBody\r\n";
        var engine = new FakeConnectorEngine
        {
            Respond = call => call.OperationId == MicrosoftAuthenticationConstants.GraphOperations.ListMessages
                ? FakeConnectorEngine.Json(new JsonObject
                {
                    ["value"] = new JsonArray(new JsonObject { ["id"] = "AAA", ["receivedDateTime"] = "2026-10-15T10:00:00Z" }),
                })
                : new ConnectorResult { Outcome = ConnectorOutcome.Succeeded, StatusCode = 200, Body = mime },
        };
        var provider = new GraphMailboxReceiveProvider(
            Monitors.Of(new GraphMailboxSettings { IsEnabled = true }),
            engine,
            Callers.Factory(),
            new PassThroughLocalizer<GraphMailboxReceiveProvider>());

        var result = await provider.ReceiveAsync(new EmailReceiveRequest { Cursor = "2026-10-15T09:00:00Z", MaxMessages = 10 }, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        var message = Assert.Single(result.Value.Messages);
        Assert.Equal("AAA", message.Id);
        Assert.Equal("Order", message.Subject);
        Assert.Equal("2026-10-15T10:00:00Z|AAA", result.Value.Cursor);

        Assert.Equal(2, engine.Calls.Count);
        Assert.Contains("receivedDateTime%20ge%202026-10-15T09%3A00%3A00Z", engine.Calls[0].Request.Path);
        Assert.StartsWith("/me/mailFolders/inbox/messages?", engine.Calls[0].Request.Path);
        Assert.Equal("/me/messages/AAA/$value", engine.Calls[1].Request.Path);
        Assert.All(engine.Calls, call => Assert.Equal(CredentialScope.Tenant, call.Scope));
    }

    [Fact]
    public async Task Messages_sharing_a_timestamp_are_neither_lost_nor_read_twice()
    {
        const string time = "2026-10-15T10:00:00Z";
        var engine = new FakeConnectorEngine
        {
            Respond = call => call.OperationId == MicrosoftAuthenticationConstants.GraphOperations.ListMessages
                ? FakeConnectorEngine.Json(new JsonObject
                {
                    // Graph answers "ge {time}" with every message at that time, in any order.
                    ["value"] = new JsonArray(
                        new JsonObject { ["id"] = "B", ["receivedDateTime"] = time },
                        new JsonObject { ["id"] = "A", ["receivedDateTime"] = time }),
                })
                : new ConnectorResult
                {
                    Outcome = ConnectorOutcome.Succeeded,
                    StatusCode = 200,
                    Body = $"Message-ID: <{call.Arguments["id"]!.GetValue<string>()}@example.com>\r\nSubject: {call.Arguments["id"]!.GetValue<string>()}\r\n\r\nBody\r\n",
                },
        };
        var provider = new GraphMailboxReceiveProvider(
            Monitors.Of(new GraphMailboxSettings { IsEnabled = true }),
            engine,
            Callers.Factory(),
            new PassThroughLocalizer<GraphMailboxReceiveProvider>());

        // One message per batch: the first batch stops inside the shared timestamp.
        var first = await provider.ReceiveAsync(new EmailReceiveRequest { MaxMessages = 1 }, TestContext.Current.CancellationToken);
        Assert.Equal("A", Assert.Single(first.Value.Messages).Id);
        Assert.Equal($"{time}|A", first.Value.Cursor);
        Assert.True(first.Value.HasMore);

        var second = await provider.ReceiveAsync(new EmailReceiveRequest { Cursor = first.Value.Cursor, MaxMessages = 1 }, TestContext.Current.CancellationToken);
        Assert.Equal("B", Assert.Single(second.Value.Messages).Id);
        Assert.Equal($"{time}|A,B", second.Value.Cursor);
        Assert.False(second.Value.HasMore);

        var third = await provider.ReceiveAsync(new EmailReceiveRequest { Cursor = second.Value.Cursor, MaxMessages = 1 }, TestContext.Current.CancellationToken);
        Assert.Empty(third.Value.Messages);
        Assert.Equal(second.Value.Cursor, third.Value.Cursor);

        // The later reads ask from the shared time ("ge") and for enough to get past the seen ids.
        var lastList = engine.Calls.Last(call => call.OperationId == MicrosoftAuthenticationConstants.GraphOperations.ListMessages);
        Assert.Contains("receivedDateTime%20ge%202026-10-15T10%3A00%3A00Z", lastList.Request.Path);
        Assert.Contains("%24top=3", lastList.Request.Path.Replace("$top", "%24top"));
    }

    [Fact]
    public void The_graph_connection_declares_the_mail_operations()
    {
        var connection = MicrosoftGraphConnectionSource.Build("common");

        Assert.Contains("Mail.ReadWrite", connection.Auth.Scopes);
        Assert.False(connection.Operations.Single(operation => operation.Id == MicrosoftAuthenticationConstants.GraphOperations.SendMail).IsSafeToRetry);
        Assert.True(connection.Operations.Single(operation => operation.Id == MicrosoftAuthenticationConstants.GraphOperations.ListMessages).IsSafeToRetry);
        Assert.True(connection.Operations.Single(operation => operation.Id == MicrosoftAuthenticationConstants.GraphOperations.GetMessageMime).IsSafeToRetry);
        Assert.False(connection.Operations.Single(operation => operation.Id == MicrosoftAuthenticationConstants.GraphOperations.DeleteMessage).IsSafeToRetry);
    }
}
