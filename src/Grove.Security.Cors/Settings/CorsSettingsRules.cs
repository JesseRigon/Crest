using Grove.Validation;
using CorsConstants = Microsoft.AspNetCore.Cors.Infrastructure.CorsConstants;

namespace Grove.Security.Cors.Settings;

/// <summary>
/// The rules of the CORS policies: a name, unique; at most one default policy; never any origin
/// together with credentials (an insecure configuration the CORS service answers with an invalid response).
/// </summary>
internal static class CorsSettingsRules
{
    public static void Validate(CorsSettings settings, ValidationErrors errors)
    {
        var policies = settings.Policies?.ToList() ?? [];
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var defaults = 0;

        for (var index = 0; index < policies.Count; index++)
        {
            var policy = policies[index];
            var key = $"policies[{index}]";

            if (string.IsNullOrWhiteSpace(policy.Name))
            {
                errors.Add($"{key}.name", "The policy name is required.");
            }
            else if (!names.Add(policy.Name.Trim()))
            {
                errors.Add($"{key}.name", $"Another policy is named '{policy.Name}'.");
            }

            if (policy.IsDefaultPolicy && ++defaults > 1)
            {
                errors.Add($"{key}.isDefaultPolicy", "Only one policy can be the default policy.");
            }

            if (policy.AllowCredentials
                && (policy.AllowAnyOrigin || (policy.AllowedOrigins?.Any(origin => origin == CorsConstants.AnyOrigin) ?? false)))
            {
                errors.Add($"{key}.allowCredentials", "Specifying AllowAnyOrigin and AllowCredentials is an insecure configuration and can result in cross-site request forgery. See https://learn.microsoft.com/aspnet/core/security/cors.");
            }
        }
    }
}
