using Microsoft.Extensions.DependencyInjection;
using Crest.DisplayManagement.Liquid;
using Crest.Environment.Shell.Configuration;
using Crest.Modules;
using Crest.ResourceManagement;
using Crest.Resources.Liquid;
using Crest.Resources.Services;

namespace Crest.Resources;

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

        var resourceConfiguration = _shellConfiguration.GetSection("Crest_Resources");
        serviceCollection.Configure<ResourceOptions>(resourceConfiguration);

        serviceCollection.AddScoped<IResourcesTagHelperProcessor, ResourcesTagHelperProcessor>();
    }
}
