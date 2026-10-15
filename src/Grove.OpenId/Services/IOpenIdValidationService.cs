using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using Grove.OpenId.Settings;

namespace Grove.OpenId.Services;

public interface IOpenIdValidationService
{
    Task<OpenIdValidationSettings> GetSettingsAsync();
    Task<OpenIdValidationSettings> LoadSettingsAsync();
    Task UpdateSettingsAsync(OpenIdValidationSettings settings);
    Task<ImmutableArray<ValidationResult>> ValidateSettingsAsync(OpenIdValidationSettings settings);
}
