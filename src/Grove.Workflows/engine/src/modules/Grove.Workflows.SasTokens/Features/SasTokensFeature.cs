using Grove.Workflows.Features.Abstractions;
using Grove.Workflows.Features.Services;
using Grove.Workflows.SasTokens.Contracts;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Workflows.SasTokens.Features;

/// <summary>
/// Adds the SAS tokens feature to the workflow runtime.
/// </summary>
/// <inheritdoc />
public class SasTokensFeature(IModule module) : FeatureBase(module)
{
    /// <summary>
    /// Configures the <see cref="IDataProtectionBuilder"/> used for setting up data protection.
    /// Defaults to leaving the host's data protection as it is: the engine runs inside a tenant
    /// whose key ring and application name the platform sets per tenant. Setting an application
    /// name here would change the discriminator of every protector in the tenant (auth cookies,
    /// sealed workflow actors, protected settings) depending on the order the options setups
    /// happen to run in, so payloads protected by one shell build would not open in the next.
    /// </summary>
    public Action<IDataProtectionBuilder> ConfigureDataProtectionBuilder { get; set; } = _ => { };

    /// <summary>
    /// Factory method to create an instance of <see cref="ITokenService"/>.
    /// Defaults to creating a <see cref="DataProtectorTokenService"/> using dependency injection.
    /// </summary>
    public Func<IServiceProvider, ITokenService> TokenService { get; set; } = sp => ActivatorUtilities.CreateInstance<DataProtectorTokenService>(sp);

    /// <inheritdoc />
    public override void Apply()
    {
        var builder = Services.AddDataProtection();
        ConfigureDataProtectionBuilder(builder);

        Services.AddScoped(TokenService);
    }
}