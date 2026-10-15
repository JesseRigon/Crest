using Fluid;
using Markdig;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Grove.ContentManagement;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentTypes.Editors;
using Grove.Data.Migration;
using Grove.Environment.Shell.Configuration;
using Grove.Indexing;
using Grove.Liquid;
using Grove.Markdown.Drivers;
using Grove.Markdown.Fields;
using Grove.Markdown.Filters;
using Grove.Markdown.Handlers;
using Grove.Markdown.Indexing;
using Grove.Markdown.Models;
using Grove.Markdown.Services;
using Grove.Markdown.Settings;
using Grove.Markdown.ViewModels;
using Grove.Modules;

namespace Grove.Markdown;

public sealed class Startup : StartupBase
{
    private const string DefaultMarkdownExtensions = "nohtml+advanced";

    private readonly IShellConfiguration _shellConfiguration;

    public Startup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<MarkdownBodyPartViewModel>();
            o.MemberAccessStrategy.Register<MarkdownFieldViewModel>();
        })
        .AddLiquidFilter<Markdownify>("markdownify");

        // Markdown Part
        services.AddContentPart<MarkdownBodyPart>()
            .UseDisplayDriver<MarkdownBodyPartDisplayDriver>()
            .AddHandler<MarkdownBodyPartHandler>();

        services.AddDataMigration<Migrations>();
        services.AddScoped<IContentPartIndexHandler, MarkdownBodyPartIndexHandler>();

        // Markdown Field
        services.AddContentField<MarkdownField>()
            .UseDisplayDriver<MarkdownFieldDisplayDriver>();

        services.AddScoped<IContentFieldIndexHandler, MarkdownFieldIndexHandler>();

        services.AddOptions<MarkdownPipelineOptions>();
        services.ConfigureMarkdownPipeline((pipeline) =>
        {
            var extensions = _shellConfiguration.GetValue("Grove_Markdown:Extensions", DefaultMarkdownExtensions);
            pipeline.Configure(extensions);
        });

        services.AddScoped<IMarkdownService, DefaultMarkdownService>();
        services.AddResourceConfiguration<ResourceManagementOptionsConfiguration>();
    }
}
