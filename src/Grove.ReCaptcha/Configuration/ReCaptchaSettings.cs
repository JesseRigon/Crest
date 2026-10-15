using System.ComponentModel.DataAnnotations;
using Grove.Settings;

namespace Grove.ReCaptcha.Configuration;

public class ReCaptchaSettings
{
    public const string GroupId = "recaptcha";

    [Display(Name = "Site Key", Description = "Your Google's reCaptcha Site Key. Get your reCaptcha at https://developers.google.com/recaptcha/", Order = 0)]
    public string SiteKey { get; set; }

    [Display(Name = "Secret Key", Description = "Your Google's reCaptcha Secret Key.", Order = 1)]
    [Setting(Secret = true)]
    public string SecretKey { get; set; }

    [ScaffoldColumn(false)]
    public string ReCaptchaScriptUri { get; set; } = Constants.ReCaptchaScriptUri;

    [ScaffoldColumn(false)]
    public string ReCaptchaApiUri { get; set; } = Constants.ReCaptchaApiUri;

    private bool? _configurationExists;

    public bool ConfigurationExists()
        => _configurationExists ??= !string.IsNullOrWhiteSpace(SiteKey)
        && !string.IsNullOrWhiteSpace(SecretKey)
        && !string.IsNullOrWhiteSpace(ReCaptchaApiUri);

    [Obsolete($"This method is obsolete and will be removed in future releases. Instead use {nameof(ConfigurationExists)}.")]
    public bool IsValid()
        => ConfigurationExists();
}
