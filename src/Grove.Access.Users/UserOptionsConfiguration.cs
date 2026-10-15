using Microsoft.Extensions.Options;
using Grove.Display.Resources.Management;

namespace Grove.Access.Users;

public sealed class UserOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest s_manifest;

    static UserOptionsConfiguration()
    {
        s_manifest = new ResourceManifest();

        s_manifest
            .DefineScript("password-generator")
            .SetUrl("~/Grove.Access.Users/Scripts/password-generator.min.js", "~/Grove.Access.Users/Scripts/password-generator.js")
            .SetVersion("1.0.0");
    }

    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(s_manifest);
    }
}
