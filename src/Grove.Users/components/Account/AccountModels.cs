namespace Grove.Users.Components;

// The account API's wire shapes (api/grove/account, Grove.Users' AccountApiController and
// ExternalAuthenticationsController), field for field.

public sealed record AccountModel(
    string UserName,
    string? Email,
    bool EmailConfirmed,
    bool HasPassword,
    bool AllowChangeEmail,
    AccountLogin[] Logins,
    AccountProvider[] OtherProviders,
    bool CanRemoveLogin);

public sealed record AccountLogin(string LoginProvider, string ProviderKey, string? ProviderDisplayName);

public sealed record AccountProvider(string Name, string DisplayName);

public sealed record AccountFieldError(string Key, string Message);

public sealed record AccountResult(bool Succeeded, AccountFieldError[]? Errors, string? Redirect = null)
{
    public static AccountResult Failed(string message) => new(false, [new AccountFieldError(string.Empty, message)]);
}

public sealed record PendingExternalLogin(
    string LoginProvider,
    string ProviderDisplayName,
    string? UserName,
    string? Email,
    bool NoPassword,
    bool NoEmail,
    bool NoUsername,
    bool RegistrationDisabled,
    string? LinkUserName);

public sealed record ExternalLoginResult(bool Succeeded, string[]? Errors, string? Redirect);
