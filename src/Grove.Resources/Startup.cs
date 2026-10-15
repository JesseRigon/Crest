using Microsoft.Extensions.DependencyInjection;
using Grove.DisplayManagement.Liquid;
using Grove.Environment.Shell.Configuration;
using Grove.Modules;
using Grove.ResourceManagement;
using Grove.Resources.Liquid;
using Grove.Resources.Services;

namespace Grove.Resources;

public sealed class Startup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    public Startup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public override void ConfigureServices(IServiceCollection serviceCollection)
    {
        serviceCollection.Configure<LiquidViewOptions>(o =>
        {
            o.LiquidViewParserConfiguration.Add(parser => parser.RegisterParserTag("meta", parser.ArgumentsListParser, MetaTag.WriteToAsync));
            o.LiquidViewParserConfiguration.Add(parser => parser.RegisterParserTag("link", parser.ArgumentsListParser, LinkTag.WriteToAsync));
            o.LiquidViewParserConfiguration.Add(parser => parser.RegisterParserTag("script", parser.ArgumentsListParser, ScriptTag.WriteToAsync));
            o.LiquidViewParserConfiguration.Add(parser => parser.RegisterParserTag("style", parser.ArgumentsListParser, StyleTag.WriteToAsync));
            o.LiquidViewParserConfiguration.Add(parser => parser.RegisterParserTag("resources", parser.ArgumentsListParser, ResourcesTag.WriteToAsync));
            o.LiquidViewParserConfiguration.Add(parser => parser.RegisterParserBlock("scriptblock", parser.ArgumentsListParser, ScriptBlock.WriteToAsync));
            o.LiquidViewParserConfiguration.Add(parser => parser.RegisterParserBlock("styleblock", parser.ArgumentsListParser, StyleBlock.WriteToAsync));
        });

        serviceCollection.AddResourceConfiguration<ResourceManagementOptionsConfiguration>();

        var resourceConfiguration = _shellConfiguration.GetSection("Grove_Resources");
        serviceCollection.Configure<ResourceOptions>(resourceConfiguration);

        serviceCollection.AddScoped<IResourcesTagHelperProcessor, ResourcesTagHelperProcessor>();
    }
}
