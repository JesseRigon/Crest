namespace Grove.Communications.Sms.Api;

// The SMS test API's wire types, shared with the SMS test page (blazor-wasm links this file).

public static class SmsTestApiPaths
{
    public const string Providers = "api/grove/sms/providers";

    public const string Test = "api/grove/sms/test";

    public const string Channel = "api/grove/sms/channel";
}

/// <summary>A test message: to <see cref="PhoneNumber"/>, through <see cref="Provider"/> (the default provider when empty).</summary>
public sealed class SmsTestRequest
{
    public string? Provider { get; set; }

    public string PhoneNumber { get; set; } = string.Empty;
}

public sealed record SmsTestResult(bool Succeeded, string[] Errors);

/// <summary>An enabled SMS provider and the connection it uses (null for one that needs none).</summary>
public sealed record SmsProviderModel(string Name, string DisplayName, string? Connection);

/// <summary>The SMS channel on the Communications page.</summary>
public sealed record SmsChannelModel(string? DefaultProvider, SmsProviderModel[] Providers);
