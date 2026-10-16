using Fluid;
using Grove.Data.Protocols.Smtp;
using Grove.Data.Protocols.Smtp.Services;
using Grove.Environment.Shell;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Grove.Communications.Tests.Email;

/// <summary>The host's pickup directory (<see cref="DefaultSmtpOptions"/>) resolves under its base, never outside it.</summary>
public class DefaultSmtpOptionsConfigurationTests
{
    [Fact]
    public void PostConfigure_Default_ResolvesDefaultPickupDirectoryLocationBase()
    {
        var appDataPath = GetRootedPath("App", "App_Data");
        var shellSettings = new ShellSettings
        {
            Name = "Default",
        };
        var sut = new DefaultSmtpOptionsConfiguration(
            new FluidParser(),
            Options.Create(new ShellOptions
            {
                ShellsApplicationDataPath = appDataPath,
            }),
            shellSettings,
            NullLogger<DefaultSmtpOptionsConfiguration>.Instance);
        var options = new DefaultSmtpOptions
        {
            DefaultSender = "admin@example.com",
            DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
        };

        sut.PostConfigure(Options.DefaultName, options);

        var expectedBasePath = Path.GetFullPath(Path.Combine(appDataPath, "Sites", shellSettings.Name, "Emails"));
        Assert.Equal(expectedBasePath, options.PickupDirectoryLocationBase);
        Assert.Equal(expectedBasePath, options.PickupDirectoryLocation);
        Assert.True(options.IsEnabled);
    }

    [Fact]
    public void PostConfigure_DisablesDefaultPickupDirectoryLocationOutsideConfiguredBase_Succeeds()
    {
        var appDataPath = GetRootedPath("App", "App_Data");
        var shellSettings = new ShellSettings
        {
            Name = "Default",
        };
        var sut = new DefaultSmtpOptionsConfiguration(
            new FluidParser(),
            Options.Create(new ShellOptions
            {
                ShellsApplicationDataPath = appDataPath,
            }),
            shellSettings,
            NullLogger<DefaultSmtpOptionsConfiguration>.Instance);
        var options = new DefaultSmtpOptions
        {
            DefaultSender = "admin@example.com",
            DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
            PickupDirectoryLocation = "..\\..\\Shared",
        };

        sut.PostConfigure(Options.DefaultName, options);

        Assert.Null(options.PickupDirectoryLocationBase);
        Assert.Null(options.PickupDirectoryLocation);
        Assert.False(options.IsEnabled);
    }

    [Fact]
    public void PostConfigure_Default_UsesSlashRootForConfiguredPickupDirectoryLocation()
    {
        var appDataPath = GetRootedPath("App", "App_Data");
        var shellSettings = new ShellSettings
        {
            Name = "Default",
        };
        var sut = new DefaultSmtpOptionsConfiguration(
            new FluidParser(),
            Options.Create(new ShellOptions
            {
                ShellsApplicationDataPath = appDataPath,
            }),
            shellSettings,
            NullLogger<DefaultSmtpOptionsConfiguration>.Instance);
        var options = new DefaultSmtpOptions
        {
            DefaultSender = "admin@example.com",
            DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
            PickupDirectoryLocationBase = @"{{ AppData }}\Drops\{{ ShellSettings.Name }}",
            PickupDirectoryLocation = "/",
        };

        sut.PostConfigure(Options.DefaultName, options);

        var expectedBasePath = Path.GetFullPath(Path.Combine(appDataPath, "Drops", shellSettings.Name));
        Assert.Equal(expectedBasePath, options.PickupDirectoryLocationBase);
        Assert.Equal(expectedBasePath, options.PickupDirectoryLocation);
        Assert.True(options.IsEnabled);
    }

    private static string GetRootedPath(params string[] segments)
        => Path.GetFullPath(Path.Combine(Path.DirectorySeparatorChar.ToString(), Path.Combine(segments)));
}
