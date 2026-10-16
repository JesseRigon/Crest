using Grove.Communications.Delivery;
using Grove.Communications.Email;
using Grove.Communications.Email.Services;
using Grove.Communications.Sms;
using Grove.Communications.Sms.Services;
using Grove.Data.Connectors;
using Grove.Infrastructure;
using Grove.Modules;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Grove.Communications.Tests;

public class DeliveryLogTests
{
    private static readonly DateTime s_now = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void A_connector_result_becomes_the_delivery_outcome_with_its_attempts()
    {
        var result = new ConnectorResult
        {
            Outcome = ConnectorOutcome.RetryAfter,
            Error = "429",
            RetryAt = new DateTimeOffset(s_now.AddMinutes(5)),
            Attempts =
            [
                new ConnectorAttempt(1, s_now, TimeSpan.FromMilliseconds(80), 429, FailureKind.RateLimited, "slow down"),
            ],
        };

        var delivery = DeliveryResult.From(result, "twilio", succeeded: false, new ResultError { Message = new("x", "Not sent") });

        Assert.False(delivery.Succeeded);
        Assert.Equal(DeliveryOutcome.Deferred, delivery.Outcome);
        Assert.Equal("twilio", delivery.ConnectionKey);
        Assert.Equal("429", delivery.Error);
        var attempt = Assert.Single(delivery.Attempts);
        Assert.Equal("RateLimited", attempt.Failure);
        Assert.Single(delivery.Errors);
    }

    [Fact]
    public void A_succeeded_send_has_no_errors()
    {
        var delivery = DeliveryResult.From(new ConnectorResult { Outcome = ConnectorOutcome.Succeeded }, "mail", succeeded: true, new ResultError { Message = new("x", "ignored") });

        Assert.True(delivery.Succeeded);
        Assert.Equal(DeliveryOutcome.Sent, delivery.Outcome);
        Assert.Empty(delivery.Errors);
    }

    [Fact]
    public async Task The_email_service_records_each_send_with_the_provider_and_attempts()
    {
        var log = new RecordingLog();
        var delivery = DeliveryResult.From(
            new ConnectorResult { Outcome = ConnectorOutcome.Failed, Error = "550", Attempts = [new ConnectorAttempt(1, s_now, TimeSpan.Zero, 550, FailureKind.Permanent, "550")] },
            "mail",
            succeeded: false,
            new ResultError { Message = new("x", "Rejected") });
        var provider = Substitute.For<IEmailProvider>();
        provider.SendAsync(Arg.Any<MailMessage>(), Arg.Any<CancellationToken>()).Returns(delivery);
        var resolver = Substitute.For<IEmailProviderResolver>();
        resolver.GetAsync(Arg.Any<string>()).Returns(ValueTask.FromResult(provider));

        var service = new DefaultEmailService(
            resolver,
            [],
            NullLogger<DefaultEmailService>.Instance,
            new PassThroughLocalizer<DefaultEmailService>(),
            Monitors.Of(new EmailOptions { DefaultProviderName = "SMTP" }),
            Clock(),
            log);

        var result = await service.SendAsync(new MailMessage { To = "a@example.com", Subject = "Invoice" }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        var record = Assert.Single(log.Records);
        Assert.Equal(DeliveryChannels.Email, record.Channel);
        Assert.Equal("SMTP", record.Provider);
        Assert.Equal("mail", record.ConnectionKey);
        Assert.Equal("a@example.com", record.Recipient);
        Assert.Equal("Invoice", record.Subject);
        Assert.Equal(DeliveryOutcome.Failed, record.Outcome);
        Assert.Equal(1, record.AttemptCount);
        Assert.Equal("550", record.Error);
        Assert.Equal(s_now, record.CreatedUtc);
    }

    [Fact]
    public async Task The_sms_service_records_a_plain_result_as_sent_once()
    {
        var log = new RecordingLog();
        var provider = Substitute.For<ISmsProvider>();
        provider.Name.Returns(new Microsoft.Extensions.Localization.LocalizedString("Log", "Log"));
        provider.SendAsync(Arg.Any<SmsMessage>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        var resolver = Substitute.For<ISmsProviderResolver>();
        resolver.GetAsync(Arg.Any<string>()).Returns(provider);

        var service = new SmsService(resolver, new PassThroughLocalizer<SmsService>(), Clock(), log);

        await service.SendAsync(new SmsMessage { To = "+15552223333", Body = "Hi" }, TestContext.Current.CancellationToken);

        var record = Assert.Single(log.Records);
        Assert.Equal(DeliveryChannels.Sms, record.Channel);
        Assert.Equal("Log", record.Provider);
        Assert.Equal(DeliveryOutcome.Sent, record.Outcome);
        Assert.Equal(1, record.AttemptCount);
        Assert.Null(record.ConnectionKey);
    }

    [Fact]
    public async Task Without_the_delivery_log_the_services_still_send()
    {
        var provider = Substitute.For<ISmsProvider>();
        provider.SendAsync(Arg.Any<SmsMessage>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        var resolver = Substitute.For<ISmsProviderResolver>();
        resolver.GetAsync(Arg.Any<string>()).Returns(provider);

        var result = await new SmsService(resolver, new PassThroughLocalizer<SmsService>(), Clock())
            .SendAsync(new SmsMessage { To = "+15552223333", Body = "Hi" }, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
    }

    private static IClock Clock()
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(s_now);
        return clock;
    }

    private sealed class RecordingLog : IDeliveryLog
    {
        public List<DeliveryRecord> Records { get; } = [];

        public Task RecordAsync(DeliveryRecord record, CancellationToken cancellationToken = default)
        {
            Records.Add(record);
            return Task.CompletedTask;
        }

        public Task<DeliveryLogPage> ListAsync(DeliveryLogQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(new DeliveryLogPage(Records.Count, Records));
    }
}
