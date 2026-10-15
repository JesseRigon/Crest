using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using Grove.Access.OpenId.Settings;

namespace Grove.Access.OpenId.Services;

public interface IOpenIdValidationService
{
    Task<OpenIdValidationSettings> GetSettingsAsync();
    Task<OpenIdValidationSettings> LoadSettingsAsync();
    Task UpdateSettingsAsync(OpenIdValidationSettings settings);
    Task<ImmutableArray<ValidationResult>> ValidateSettingsAsync(OpenIdValidationSettings settings);
}
