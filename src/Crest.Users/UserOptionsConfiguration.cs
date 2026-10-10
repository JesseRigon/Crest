using Microsoft.Extensions.Options;
using Crest.ResourceManagement;

namespace Crest.Users;

public sealed class UserOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest s_manifest;

    static UserOptionsConfiguration()
    {
        s_manifest = new ResourceManifest();

        s_manifest
            .DefineScript("password-generator")
            .SetUrl("~/Crest.Users/Scripts/password-generator.min.js", "~/Crest.Users/Scripts/password-generator.js")
            .SetVersion("1.0.0");

        s_manifest
            .DefineScript("qrcode")
            .SetUrl("~/Crest.Users/Scripts/qrcode.min.js", "~/Crest.Users/Scripts/qrcode.js")
            .SetVersion("1.0.0");
    }

    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(s_manifest);
    }
}
