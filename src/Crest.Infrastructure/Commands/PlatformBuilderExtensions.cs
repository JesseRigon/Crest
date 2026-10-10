using Crest.Environment.Commands;
using Crest.Environment.Commands.Builtin;
using Crest.Environment.Commands.Parameters;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class PlatformBuilderExtensions
{
    /// <summary>
    /// Adds host level services to provide CLI commands.
    /// </summary>
    public static PlatformBuilder AddCommands(this PlatformBuilder builder)
    {
        var services = builder.ApplicationServices;

        services.AddScoped<ICommandManager, DefaultCommandManager>();
        services.AddScoped<ICommandHandler, HelpCommand>();

        services.AddScoped<ICommandParametersParser, CommandParametersParser>();
        services.AddScoped<ICommandParser, CommandParser>();

        return builder;
    }
}
