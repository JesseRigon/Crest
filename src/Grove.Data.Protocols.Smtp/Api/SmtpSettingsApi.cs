using Grove.Api;
using Microsoft.AspNetCore.Http;
using Grove.Communications.Email.Services;
using Grove.Data.Protocols.Smtp.Services;
using Grove.Entities;
using Grove.Environment.Options;
using Grove.Infrastructure.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Grove.Communications.Email;

namespace Grove.Data.Protocols.Smtp.Api;

/// <summary>The tenant's SMTP provider as the native email settings page edits it. The password never
/// leaves the server: <see cref="HasPassword"/> says whether one is stored.</summary>
public sealed record SmtpSettingsModel(
    bool IsEnabled,
    string? DefaultSender,
    SmtpDeliveryMethod DeliveryMethod,
    string? PickupDirectoryLocation,
    string? Host,
    int Port,
    bool AutoSelectEncryption,
    SmtpEncryptionMethod EncryptionMethod,
    bool RequireCredentials,
    bool UseDefaultCredentials,
    string? UserName,
    bool HasPassword,
    string? ProxyHost,
    int ProxyPort,
    bool IgnoreInvalidSslCertificate);

/// <param name="Password">A new password; empty keeps the stored one.</param>
public sealed record SmtpSettingsUpdate(
    bool IsEnabled,
    string? DefaultSender,
    SmtpDeliveryMethod DeliveryMethod,
    string? PickupDirectoryLocation,
    string? Host,
    int Port,
    bool AutoSelectEncryption,
    SmtpEncryptionMethod EncryptionMethod,
    bool RequireCredentials,
    bool UseDefaultCredentials,
    string? UserName,
    string? Password,
    string? ProxyHost,
    int ProxyPort,
    bool IgnoreInvalidSslCertificate);

/// <summary>
/// <c>api/grove/settings/email/smtp</c>: Settings › Email › SMTP (the native page): the same rules
/// the retired settings driver applied.
/// </summary>
public sealed class SmtpSettingsApi : ApiContributor
{
    public override string Name => "SmtpSettings";

    public override string FeatureId => "Grove.Data.Protocols.Smtp";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("settings/email/smtp");
        routes.MapGet("", ApiHandlers.Handle<SmtpSettingsHandlers>(h => h.GetAsync()));
        routes.MapPut("", (HttpContext http, [FromBody] SmtpSettingsUpdate update) => ApiHandlers.For<SmtpSettingsHandlers>(http).PutAsync(update));
    }
}

public sealed class SmtpSettingsHandlers(
    IAuthorizationService authorization,
    ISiteService siteService,
    IDataProtectionProvider dataProtectionProvider,
    IEmailAddressValidator emailValidator,
    IOptionsUpdateNotifier optionsUpdateNotifier) : ApiHandlers
{
    public async Task<IResult> GetAsync()
    {
        if (!await authorization.AuthorizeAsync(User, EmailPermissions.ManageEmailSettings))
        {
            return Forbid();
        }

        return Ok(ToModel((await siteService.GetSiteSettingsAsync()).GetOrCreate<SmtpSettings>()));
    }

    public async Task<IResult> PutAsync([FromBody] SmtpSettingsUpdate update)
    {
        if (!await authorization.AuthorizeAsync(User, EmailPermissions.ManageEmailSettings))
        {
            return Forbid();
        }

        if (update.IsEnabled)
        {
            if (string.IsNullOrWhiteSpace(update.DefaultSender) || !emailValidator.Validate(update.DefaultSender))
            {
                return BadRequest("A valid default sender is required.");
            }

            if (update.DeliveryMethod == SmtpDeliveryMethod.Network && string.IsNullOrWhiteSpace(update.Host))
            {
                return BadRequest("The host name is required for network delivery.");
            }

            if (update.DeliveryMethod == SmtpDeliveryMethod.SpecifiedPickupDirectory
                && !SmtpPickupDirectoryResolver.IsValidPickupDirectoryLocation(update.PickupDirectoryLocation))
            {
                return BadRequest("The pickup directory location is invalid.");
            }
        }

        var site = await siteService.LoadSiteSettingsAsync();
        var settings = site.GetOrCreate<SmtpSettings>();
        var emailSettings = site.GetOrCreate<EmailSettings>();

        if (!update.IsEnabled)
        {
            settings.IsEnabled = false;
            if (emailSettings.DefaultProviderName == SmtpEmailProvider.TechnicalName)
            {
                emailSettings.DefaultProviderName = null;
            }
        }
        else
        {
            settings.IsEnabled = true;
            settings.DefaultSender = update.DefaultSender;
            settings.DeliveryMethod = update.DeliveryMethod;
            settings.PickupDirectoryLocation = string.IsNullOrWhiteSpace(update.PickupDirectoryLocation)
                ? SmtpPickupDirectoryResolver.DefaultPickupDirectoryLocation
                : update.PickupDirectoryLocation;
            settings.Host = update.Host;
            settings.Port = update.Port;
            settings.AutoSelectEncryption = update.AutoSelectEncryption;
            settings.EncryptionMethod = update.EncryptionMethod;
            settings.RequireCredentials = update.RequireCredentials;
            settings.UseDefaultCredentials = update.UseDefaultCredentials;
            settings.UserName = update.UserName;
            settings.ProxyHost = update.ProxyHost;
            settings.ProxyPort = update.ProxyPort;
            settings.IgnoreInvalidSslCertificate = update.IgnoreInvalidSslCertificate;
            if (!string.IsNullOrWhiteSpace(update.Password))
            {
                settings.Password = dataProtectionProvider.CreateProtector(SmtpOptionsConfiguration.ProtectorName).Protect(update.Password);
            }

            // Enabling the only provider makes it the default.
            if (string.IsNullOrEmpty(emailSettings.DefaultProviderName))
            {
                emailSettings.DefaultProviderName = SmtpEmailProvider.TechnicalName;
            }
        }

        site.Put(settings);
        site.Put(emailSettings);
        await siteService.UpdateSiteSettingsAsync(site);
        optionsUpdateNotifier
            .RequestUpdate<SmtpOptions>()
            .RequestUpdate<EmailProviderOptions>()
            .RequestUpdate<EmailOptions>();

        return Ok(ToModel(settings));
    }

    private static SmtpSettingsModel ToModel(SmtpSettings settings) => new(
        settings.IsEnabled == true,
        settings.DefaultSender,
        settings.DeliveryMethod,
        string.IsNullOrWhiteSpace(settings.PickupDirectoryLocation) ? SmtpPickupDirectoryResolver.DefaultPickupDirectoryLocation : settings.PickupDirectoryLocation,
        settings.Host,
        settings.Port,
        settings.AutoSelectEncryption,
        settings.EncryptionMethod,
        settings.RequireCredentials,
        settings.UseDefaultCredentials,
        settings.UserName,
        !string.IsNullOrEmpty(settings.Password),
        settings.ProxyHost,
        settings.ProxyPort,
        settings.IgnoreInvalidSslCertificate);
}
