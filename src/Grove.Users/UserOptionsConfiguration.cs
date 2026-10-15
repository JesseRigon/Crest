using Microsoft.Extensions.Options;
using Grove.ResourceManagement;

namespace Grove.Users;

public sealed class UserOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest s_manifest;

    static UserOptionsConfiguration()
    {
        s_manifest = new ResourceManifest();

        s_manifest
            .DefineScript("password-generator")
            .SetUrl("~/Grove.Users/Scripts/password-generator.min.js", "~/Grove.Users/Scripts/password-generator.js")
            .SetVersion("1.0.0");
    }

    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(s_manifest);
    }
}
