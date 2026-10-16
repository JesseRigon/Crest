using System.Text;
using Grove.Communications.Email;
using Grove.Data.Connectors;
using Grove.Data.Protocols.Imap;
using Grove.Data.Protocols.Pop;
using Grove.Data.Protocols.Smtp.Receiving;
using Xunit;

namespace Grove.Communications.Tests.Email;

public class MailboxReceiveProviderTests
{
    private const string Mime = "Message-ID: <1@example.com>\r\nFrom: a@example.com\r\nTo: b@example.com\r\nSubject: Invoice\r\n\r\nPlease pay.\r\n";

    [Fact]
    public async Task Imap_reads_through_its_connection_and_returns_the_messages_and_cursor()
    {
        var engine = new FakeConnectorEngine
        {
            Respond = _ => FakeConnectorEngine.Json(Mailbox.FetchResponse([("42", Encoding.ASCII.GetBytes(Mime))], "7:42", hasMore: true)),
        };
        var provider = new ImapEmailReceiveProvider(
            Monitors.Of(new ImapSettings { IsEnabled = true, ConnectionKey = "inbox", Folder = "Billing" }),
            engine,
            Callers.Factory(),
            new PassThroughLocalizer<ImapEmailReceiveProvider>());

        var result = await provider.ReceiveAsync(new EmailReceiveRequest { Cursor = "7:40", MaxMessages = 10 }, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        var message = Assert.Single(result.Value.Messages);
        Assert.Equal("42", message.Id);
        Assert.Equal("Invoice", message.Subject);
        Assert.Equal("1@example.com", message.MessageId);
        Assert.Equal("7:42", result.Value.Cursor);
        Assert.True(result.Value.HasMore);

        var call = Assert.Single(engine.Calls);
        Assert.Equal("inbox", call.ConnectionKey);
        Assert.Same(Callers.System, call.Caller);
        Assert.Equal(Mailbox.FetchMethod, call.Request.Method);
        Assert.Equal("Billing", call.Arguments[Mailbox.Folder]!.GetValue<string>());
        Assert.Equal("7:40", call.Arguments[Mailbox.Cursor]!.GetValue<string>());
        Assert.Equal(10, call.Arguments[Mailbox.Max]!.GetValue<int>());
    }

    [Fact]
    public async Task Pop_deletes_by_id_through_its_connection()
    {
        var engine = new FakeConnectorEngine();
        var provider = new PopEmailReceiveProvider(
            Monitors.Of(new PopSettings { IsEnabled = true, ConnectionKey = "maildrop" }),
            engine,
            Callers.Factory(),
            new PassThroughLocalizer<PopEmailReceiveProvider>());

        var result = await provider.DeleteAsync(["uid-1", "uid-2"], TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        var call = Assert.Single(engine.Calls);
        Assert.Equal("maildrop", call.ConnectionKey);
        Assert.Equal(Mailbox.DeleteMethod, call.Request.Method);
        Assert.Equal(["uid-1", "uid-2"], call.Arguments[Mailbox.Ids]!.AsArray().Select(node => node!.GetValue<string>()));
    }

    [Fact]
    public async Task A_disabled_receive_provider_calls_nothing()
    {
        var engine = new FakeConnectorEngine();
        var provider = new ImapEmailReceiveProvider(
            Monitors.Of(new ImapSettings { IsEnabled = false, ConnectionKey = "inbox" }),
            engine,
            Callers.Factory(),
            new PassThroughLocalizer<ImapEmailReceiveProvider>());

        var result = await provider.ReceiveAsync(new EmailReceiveRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Empty(engine.Calls);
    }

    [Fact]
    public async Task A_failed_read_is_a_failed_result()
    {
        var engine = new FakeConnectorEngine { Respond = _ => new ConnectorResult { Outcome = ConnectorOutcome.NotAuthorized } };
        var provider = new PopEmailReceiveProvider(
            Monitors.Of(new PopSettings { IsEnabled = true, ConnectionKey = "maildrop" }),
            engine,
            Callers.Factory(),
            new PassThroughLocalizer<PopEmailReceiveProvider>());

        var result = await provider.ReceiveAsync(new EmailReceiveRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void The_read_operation_is_safe_and_the_delete_is_not()
    {
        Assert.True(Mailbox.FetchOperation.IsSafeToRetry);
        Assert.False(Mailbox.DeleteOperation.IsSafeToRetry);
    }
}
