using Grove.Api;
using Microsoft.AspNetCore.Http;
using Grove.Communications.Email.Services;
using Grove.Entities;
using Grove.Environment.Options;
using Grove.Infrastructure.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Grove.Communications.Email.Api;

/// <summary>An enabled email provider the tenant can choose as its default.</summary>
public sealed record EmailProviderModel(string Name, string DisplayName);

/// <param name="DefaultProvider">The provider messages go through; null for none.</param>
public sealed record EmailSettingsModel(string? DefaultProvider, IReadOnlyList<EmailProviderModel> Providers);

public sealed record EmailSettingsUpdate(string? DefaultProvider);

/// <summary>A test message; an empty <see cref="Provider"/> sends through the default.</summary>
public sealed record EmailTestRequest(string? Provider, string To, string? From, string? Cc, string? Bcc, string? ReplyTo, string? Subject, string? Body);

public sealed record EmailTestResult(bool Succeeded, IReadOnlyList<string> Errors);

/// <summary>
/// <c>api/grove/settings/email</c>: Settings › Email (the native page, Pages/EmailSettings.razor):
/// the default provider and a test send.
/// </summary>
public sealed class EmailSettingsApi : ApiContributor
{
    public override string Name => "EmailSettings";

    public override string FeatureId => "Grove.Communications.Email";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("settings/email");
        routes.MapGet("", ApiHandlers.Handle<EmailSettingsHandlers>(h => h.GetAsync()));
        routes.MapPut("", (HttpContext http, [FromBody] EmailSettingsUpdate update) => ApiHandlers.For<EmailSettingsHandlers>(http).PutAsync(update));
        routes.MapPost("test", (HttpContext http, [FromBody] EmailTestRequest request) => ApiHandlers.For<EmailSettingsHandlers>(http).TestAsync(request));
    }
}

public sealed class EmailSettingsHandlers(
    IAuthorizationService authorization,
    ISiteService siteService,
    IOptionsMonitor<EmailOptions> emailOptions,
    IOptionsMonitor<EmailProviderOptions> providerOptions,
    IEmailProviderResolver providerResolver,
    IEmailService emailService,
    IOptionsUpdateNotifier optionsUpdateNotifier) : ApiHandlers
{
    public async Task<IResult> GetAsync()
    {
        if (!await authorization.AuthorizeAsync(User, EmailPermissions.ManageEmailSettings))
        {
            return Forbid();
        }

        var settings = (await siteService.GetSiteSettingsAsync()).GetOrCreate<EmailSettings>();
        return Ok(new EmailSettingsModel(settings.DefaultProviderName ?? emailOptions.CurrentValue.DefaultProviderName, await ProvidersAsync()));
    }

    public async Task<IResult> PutAsync([FromBody] EmailSettingsUpdate update)
    {
        if (!await authorization.AuthorizeAsync(User, EmailPermissions.ManageEmailSettings))
        {
            return Forbid();
        }

        var providers = await ProvidersAsync();
        var requested = string.IsNullOrWhiteSpace(update.DefaultProvider) ? null : update.DefaultProvider;
        if (requested is not null && providers.All(provider => provider.Name != requested))
        {
            return BadRequest($"'{requested}' is not an enabled email provider.");
        }

        var site = await siteService.LoadSiteSettingsAsync();
        var settings = site.GetOrCreate<EmailSettings>();
        if (settings.DefaultProviderName != requested)
        {
            site.Alter<EmailSettings>(value => value.DefaultProviderName = requested);
            await siteService.UpdateSiteSettingsAsync(site);
            optionsUpdateNotifier.RequestUpdate<EmailOptions>();
        }

        return Ok(new EmailSettingsModel(requested, providers));
    }

    /// <summary>Sends a test message (Tools › Testing › Email Test).</summary>
    public async Task<IResult> TestAsync([FromBody] EmailTestRequest request)
    {
        if (!await authorization.AuthorizeAsync(User, EmailPermissions.ManageEmailSettings))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.To))
        {
            return BadRequest("A recipient is required.");
        }

        var message = new MailMessage
        {
            To = request.To,
            Cc = request.Cc,
            Bcc = request.Bcc,
            ReplyTo = request.ReplyTo,
        };

        if (!string.IsNullOrWhiteSpace(request.From))
        {
            message.From = request.From;
        }

        if (!string.IsNullOrWhiteSpace(request.Subject))
        {
            message.Subject = request.Subject;
        }

        if (!string.IsNullOrWhiteSpace(request.Body))
        {
            message.TextBody = request.Body;
        }

        try
        {
            var result = await emailService.SendAsync(message, string.IsNullOrWhiteSpace(request.Provider) ? null : request.Provider);
            return Ok(new EmailTestResult(result.Succeeded, [.. result.Errors.Select(error => $"{error.Key}: {error.Message.Value}".TrimStart(':', ' '))]));
        }
        catch (InvalidEmailProviderException)
        {
            return Ok(new EmailTestResult(false, ["The selected provider is invalid or no longer enabled."]));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Ok(new EmailTestResult(false, [$"Unable to send the message using the selected provider: {exception.Message}"]));
        }
    }

    private async Task<IReadOnlyList<EmailProviderModel>> ProvidersAsync()
    {
        var list = new List<EmailProviderModel>();
        foreach (var entry in providerOptions.CurrentValue.Providers.Where(entry => entry.Value.IsEnabled))
        {
            var provider = await providerResolver.GetAsync(entry.Key);
            list.Add(new EmailProviderModel(entry.Key, provider.DisplayName.Value));
        }

        return [.. list.OrderBy(provider => provider.DisplayName, StringComparer.CurrentCultureIgnoreCase)];
    }
}
