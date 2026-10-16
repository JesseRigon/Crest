using System.Net.Sockets;
using System.Text.Json.Nodes;
using Grove.Communications.Email;
using Grove.Data.Connectors;
using Grove.Data.Protocols.Smtp;
using Grove.Data.Protocols.Smtp.Services;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Grove.Communications.Tests.Email;

public class SmtpEmailProviderTests
{
    [Fact]
    public async Task Send_runs_an_smtp_send_on_the_picked_connection_as_the_system_actor()
    {
        var engine = new FakeConnectorEngine();
        var provider = CreateProvider(engine, new SmtpOptions { IsEnabled = true, DefaultSender = "noreply@example.com", ConnectionKey = "mail" });

        var result = await provider.SendAsync(new MailMessage { To = "someone@example.com", Subject = "Hello", TextBody = "Body" }, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        var call = Assert.Single(engine.Calls);
        Assert.Equal("mail", call.ConnectionKey);
        Assert.Same(Callers.System, call.Caller);
        Assert.Equal(SmtpConnectorProtocol.SendMethod, call.Request.Method);
        Assert.Equal(SmtpConnectorProtocol.MessageContentType, call.Request.ContentType);
        Assert.Contains("To: someone@example.com", call.Request.Body);
        Assert.Contains("From: noreply@example.com", call.Request.Body);
        Assert.Contains("Subject: Hello", call.Request.Body);
    }

    [Fact]
    public async Task A_disabled_provider_or_one_without_a_connection_calls_nothing()
    {
        var engine = new FakeConnectorEngine();

        var disabled = await CreateProvider(engine, new SmtpOptions { IsEnabled = false, ConnectionKey = "mail" })
            .SendAsync(new MailMessage { To = "someone@example.com" }, TestContext.Current.CancellationToken);
        var unconnected = await CreateProvider(engine, new SmtpOptions { IsEnabled = true })
            .SendAsync(new MailMessage { To = "someone@example.com" }, TestContext.Current.CancellationToken);

        Assert.False(disabled.Succeeded);
        Assert.False(unconnected.Succeeded);
        Assert.Empty(engine.Calls);
    }

    [Fact]
    public async Task A_failed_call_is_a_failed_result_with_the_engine_error()
    {
        var engine = new FakeConnectorEngine
        {
            Respond = _ => new ConnectorResult { Outcome = ConnectorOutcome.Failed, Error = "550 mailbox unavailable" },
        };

        var result = await CreateProvider(engine, new SmtpOptions { IsEnabled = true, DefaultSender = "noreply@example.com", ConnectionKey = "mail" })
            .SendAsync(new MailMessage { To = "someone@example.com", TextBody = "Body" }, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Contains("550 mailbox unavailable", Assert.Single(result.Errors).Message.Value);
    }

    [Fact]
    public void The_send_operation_is_unsafe_to_retry()
    {
        Assert.False(SmtpConnectorProtocol.SendOperation.IsSafeToRetry);
    }

    [Fact]
    public void A_refused_command_proves_the_message_was_not_taken()
    {
        var busy = SmtpConnectorProtocol.ClassifySending(new SmtpCommandException(SmtpErrorCode.RecipientNotAccepted, SmtpStatusCode.MailboxBusy, "busy"));
        var rejected = SmtpConnectorProtocol.ClassifySending(new SmtpCommandException(SmtpErrorCode.MessageNotAccepted, SmtpStatusCode.MailboxUnavailable, "no"));
        var broken = SmtpConnectorProtocol.ClassifySending(new IOException("connection reset"));

        Assert.Equal(new FailureClassification(FailureKind.Transient, NotSent: true), busy);
        Assert.Equal(new FailureClassification(FailureKind.Permanent, NotSent: true), rejected);
        Assert.Equal(FailureKind.Transient, broken.Kind);
        Assert.False(broken.NotSent);
    }

    [Fact]
    public void Failures_before_the_mail_transaction_are_not_sent()
    {
        Assert.Equal(new FailureClassification(FailureKind.Unauthorized, NotSent: true), MailServerConnection.ClassifyBeforeSend(new AuthenticationException("bad password")));
        Assert.Equal(new FailureClassification(FailureKind.Transient, NotSent: true), MailServerConnection.ClassifyBeforeSend(new SocketException((int)SocketError.ConnectionRefused)));
        Assert.Equal(new FailureClassification(FailureKind.Transient, NotSent: true), MailServerConnection.ClassifyBeforeSend(new SocketException((int)SocketError.HostNotFound)));
        Assert.Equal(new FailureClassification(FailureKind.Refused, NotSent: true), MailServerConnection.ClassifyBeforeSend(new ConnectorRefusedException("no")));
    }

    [Fact]
    public async Task The_protocol_refuses_a_request_without_a_message()
    {
        var response = await new SmtpConnectorProtocol().SendAsync(Context(new ConnectorRequest("SEND", null)), TestContext.Current.CancellationToken);

        Assert.Equal(new FailureClassification(FailureKind.Refused, NotSent: true), response.Failure);
    }

    [Fact]
    public async Task The_protocol_refuses_a_connection_without_a_host()
    {
        var response = await new SmtpConnectorProtocol().SendAsync(Context(Message()), TestContext.Current.CancellationToken);

        Assert.Equal(new FailureClassification(FailureKind.Refused, NotSent: true), response.Failure);
    }

    [Fact]
    public async Task The_protocol_refuses_a_private_server_unless_the_shell_allows_private_networks()
    {
        var context = Context(Message(), new Dictionary<string, string> { [MailConnectionSettings.Host] = "127.0.0.1", [MailConnectionSettings.Port] = "25" });

        var response = await new SmtpConnectorProtocol().SendAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(new FailureClassification(FailureKind.Refused, NotSent: true), response.Failure);
    }

    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("10.1.2.3", true)]
    [InlineData("172.20.0.1", true)]
    [InlineData("192.168.1.1", true)]
    [InlineData("169.254.0.1", true)]
    [InlineData("100.64.0.1", true)]
    [InlineData("::1", true)]
    [InlineData("fd00::1", true)]
    [InlineData("8.8.8.8", false)]
    [InlineData("2001:4860:4860::8888", false)]
    public void Non_public_addresses_are_recognized(string address, bool nonPublic)
    {
        Assert.Equal(nonPublic, MailServerConnection.IsNonPublic(System.Net.IPAddress.Parse(address)));
    }

    private static ConnectorRequest Message() => new(
        SmtpConnectorProtocol.SendMethod,
        null,
        "From: a@example.com\r\nTo: b@example.com\r\nSubject: Hi\r\n\r\nBody\r\n",
        SmtpConnectorProtocol.MessageContentType);

    private static IProtocolCallContext Context(ConnectorRequest request, Dictionary<string, string> settings = null)
    {
        var context = Substitute.For<IProtocolCallContext>();
        context.Request.Returns(request);
        context.Arguments.Returns((JsonObject)null);
        context.Connection.Returns(new ConnectionDefinition
        {
            Key = "mail",
            DisplayName = "Mail",
            Protocol = ConnectorProtocols.Smtp,
            Settings = settings ?? new Dictionary<string, string>(),
        });
        context.Services.Returns(Substitute.For<IServiceProvider>());
        return context;
    }

    private static SmtpEmailProvider CreateProvider(FakeConnectorEngine engine, SmtpOptions options)
    {
        var validator = Substitute.For<IEmailAddressValidator>();
        validator.Validate(Arg.Any<string>()).Returns(true);

        return new SmtpEmailProvider(
            Monitors.Of(options),
            engine,
            Callers.Factory(),
            validator,
            NullLogger<SmtpEmailProvider>.Instance,
            new PassThroughLocalizer<SmtpEmailProvider>());
    }
}
