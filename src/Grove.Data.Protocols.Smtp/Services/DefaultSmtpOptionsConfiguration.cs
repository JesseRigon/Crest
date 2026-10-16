using Fluid;
using Grove.Environment.Shell;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Grove.Data.Protocols.Smtp.Services;

/// <summary>Resolves the host's pickup directory (<see cref="DefaultSmtpOptions"/>) under its base.</summary>
public sealed class DefaultSmtpOptionsConfiguration(
    FluidParser fluidParser,
    IOptions<ShellOptions> shellOptions,
    ShellSettings shellSettings,
    ILogger<DefaultSmtpOptionsConfiguration> logger) : IPostConfigureOptions<DefaultSmtpOptions>
{
    public void PostConfigure(string name, DefaultSmtpOptions options)
    {
        if (options.DeliveryMethod != SmtpDeliveryMethod.SpecifiedPickupDirectory)
        {
            options.IsEnabled = false;
            return;
        }

        try
        {
            SmtpPickupDirectoryResolver.ConfigurePickupDirectory(
                options,
                options.PickupDirectoryLocationBase,
                fluidParser,
                shellOptions.Value,
                shellSettings);
        }
        catch (Exception exception)
        {
            options.PickupDirectoryLocationBase = null;
            options.PickupDirectoryLocation = null;
            logger.LogCritical(exception, "Unable to resolve the SMTP pickup directory location.");
        }

        options.IsEnabled = options.PickupDirectoryLocation is not null && options.ConfigurationExists();
    }
}
