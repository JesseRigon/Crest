using Crest.Https.Settings;

namespace Crest.Https.ViewModels;

public class HttpsSettingsViewModel
{
    public bool IsHttpsRequest { get; set; }
    public HttpStrictTransportSecurityMode StrictTransportSecurityMode { get; set; }
    public bool RequireHttps { get; set; }
    public bool RequireHttpsPermanent { get; set; }
    public int? SslPort { get; set; }
}
