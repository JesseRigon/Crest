namespace Grove.Users.Components;

// The two-factor API's wire shapes (api/grove/two-factor, Grove.Users' TwoFactorApi), field for field.

public sealed record TwoFactorMethodState(string Provider, bool IsEnabled);

public sealed record TwoFactorState(
    bool IsEnabled,
    bool CanDisable,
    bool IsRequired,
    bool IsBrowserRemembered,
    int RecoveryCodesLeft,
    string? PreferredProvider,
    TwoFactorMethodState[] Methods,
    bool EmailConfirmed,
    string? PhoneNumber,
    bool AllowChangingPhoneNumber);

public sealed record AuthenticatorEnrollment(string SharedKey, string AuthenticatorUri);

/// <summary>An action's outcome: its message and, when it made them, the new recovery codes (shown once).</summary>
public sealed record TwoFactorActionResult(bool Succeeded, string? Message, string[]? RecoveryCodes = null);

internal static class TwoFactorApi
{
    public const string Root = "api/grove/two-factor";

    /// <summary>Runs <paramref name="action"/>; its outcome, or a generic failure when the call failed.</summary>
    public static async Task<TwoFactorActionResult> PostAsync(HttpClient http, string action, object? body)
    {
        var (result, refusal) = await SignInApi.PostAsync<TwoFactorActionResult, TwoFactorActionResult>(http, $"{Root}/{action}", body);
        return result ?? refusal ?? new TwoFactorActionResult(false, "The request could not be completed.");
    }
}
