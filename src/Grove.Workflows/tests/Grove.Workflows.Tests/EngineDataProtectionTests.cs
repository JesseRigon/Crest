using Grove.Workflows.Features.Services;
using Grove.Workflows.SasTokens.Features;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Grove.Workflows.Tests;

// The engine runs inside a tenant whose data protection the platform configures (key ring and
// application name per tenant). The engine must leave it alone: an application name set here won
// or lost by options-setup order, so actors sealed in one shell build did not open in the next.
public sealed class EngineDataProtectionTests
{
    [Fact]
    public void SasTokens_KeepTheTenantsApplicationName()
    {
        var services = new ServiceCollection();
        var builder = services.AddDataProtection().SetApplicationName("Default");

        new SasTokensFeature(Substitute.For<IModule>()).ConfigureDataProtectionBuilder(builder);

        using var provider = services.BuildServiceProvider();
        Assert.Equal("Default", provider.GetRequiredService<IOptions<DataProtectionOptions>>().Value.ApplicationDiscriminator);
    }
}
