using Fluid;
using Markdig;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Grove.Contents.Management;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Data.Migration;
using Grove.Environment.Shell.Configuration;
using Grove.Data.Indexing;
using Grove.Display.Liquid;
using Grove.Contents.Markdown.Drivers;
using Grove.Contents.Markdown.Fields;
using Grove.Contents.Markdown.Filters;
using Grove.Contents.Markdown.Handlers;
using Grove.Contents.Markdown.Indexing;
using Grove.Contents.Markdown.Models;
using Grove.Contents.Markdown.Services;
using Grove.Contents.Markdown.Settings;
using Grove.Contents.Markdown.ViewModels;
using Grove.Modules;

namespace Grove.Contents.Markdown;

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
            .UseDisplayDriver<MarkdownFieldDisplayDriver>()
            .AddHandler<MarkdownFieldHandler>();

        services.AddScoped<IContentFieldIndexHandler, MarkdownFieldIndexHandler>();

        services.AddOptions<MarkdownPipelineOptions>();
        services.ConfigureMarkdownPipeline((pipeline) =>
        {
            var extensions = _shellConfiguration.GetValue("Grove_Markdown:Extensions", DefaultMarkdownExtensions);
            pipeline.Configure(extensions);
        });

        services.AddScoped<IMarkdownService, DefaultMarkdownService>();
    }
}
