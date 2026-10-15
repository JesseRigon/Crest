using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Grove.Data.Protocols.Https.Settings;

/// <summary>
/// Stores the HTTPS site settings for a tenant.
/// </summary>
public class HttpsSettings
{
    public const string GroupId = "Https";

    /// <summary>
    /// Gets or sets the legacy HSTS toggle retained only so stored settings can be migrated to <see cref="StrictTransportSecurityMode"/>.
    /// </summary>
    [JsonIgnore]
    [Obsolete("This property is obsolete and will be removed in future releases. Use StrictTransportSecurityMode instead.")]
    public bool EnableStrictTransportSecurity { get; set; }

    /// <summary>
    /// Gets or sets how HTTP Strict Transport Security (HSTS) is applied.
    /// </summary>
    [Display(Name = "HSTS mode", Description = "Determines whether HTTP Strict Transport Security (HSTS) headers are disabled by default, enabled explicitly, or follow the current environment. Use HSTS with caution, as browsers may stop connecting over HTTP after receiving the header if HTTPS later becomes unavailable.", Order = 1)]
    public HttpStrictTransportSecurityMode StrictTransportSecurityMode { get; set; } = HttpStrictTransportSecurityMode.Disabled;

    /// <summary>
    /// Gets or sets a value indicating whether all requests should be redirected to HTTPS.
    /// </summary>
    [Display(Name = "Require HTTPS", Description = "Require HTTPS for all requests", Order = 0)]
    public bool RequireHttps { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether HTTPS redirects should use a permanent redirect status code.
    /// </summary>
    [Display(Name = "Require HTTPS Permanent", Description = "Specifies whether a permanent redirect, 308 Moved Permanently, should be used instead of a temporary redirect, 307 Found, when redirecting to HTTPS", Order = 2)]
    public bool RequireHttpsPermanent { get; set; }

    /// <summary>
    /// Gets or sets the HTTPS port to use for redirection when it cannot be inferred automatically.
    /// </summary>
    [Display(Name = "SSL Port", Description = "The SSL Port Number, leave empty to let the middleware try to determinate it.", Order = 3)]
    [Range(1, 65535)]
    public int? SslPort { get; set; }
}
