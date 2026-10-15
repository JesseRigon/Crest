using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using Grove.OpenId.Settings;

namespace Grove.OpenId.Services;

public interface IOpenIdClientService
{
    Task<OpenIdClientSettings> GetSettingsAsync();
    Task<OpenIdClientSettings> LoadSettingsAsync();
    Task UpdateSettingsAsync(OpenIdClientSettings settings);
    Task<ImmutableArray<ValidationResult>> ValidateSettingsAsync(OpenIdClientSettings settings);
}
