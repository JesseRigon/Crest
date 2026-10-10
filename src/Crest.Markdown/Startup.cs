using Fluid;
using Markdig;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Crest.ContentManagement;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentTypes.Editors;
using Crest.Data.Migration;
using Crest.Environment.Shell.Configuration;
using Crest.Indexing;
using Crest.Liquid;
using Crest.Markdown.Drivers;
using Crest.Markdown.Fields;
using Crest.Markdown.Filters;
using Crest.Markdown.Handlers;
using Crest.Markdown.Indexing;
using Crest.Markdown.Models;
using Crest.Markdown.Services;
using Crest.Markdown.Settings;
using Crest.Markdown.ViewModels;
using Crest.Modules;

namespace Crest.Markdown;

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

        services.AddScoped<IContentTypePartDefinitionDisplayDriver, MarkdownBodyPartSettingsDisplayDriver>();
        services.AddScoped<IContentTypePartDefinitionDisplayDriver, MarkdownBodyPartWysiwygEditorSettingsDriver>();
        services.AddDataMigration<Migrations>();
        services.AddScoped<IContentPartIndexHandler, MarkdownBodyPartIndexHandler>();

        // Markdown Field
        services.AddContentField<MarkdownField>()
            .UseDisplayDriver<MarkdownFieldDisplayDriver>();

        services.AddScoped<IContentPartFieldDefinitionDisplayDriver, MarkdownFieldSettingsDriver>();
        services.AddScoped<IContentPartFieldDefinitionDisplayDriver, MarkdownFieldWysiwygEditorSettingsDriver>();
        services.AddScoped<IContentFieldIndexHandler, MarkdownFieldIndexHandler>();

        services.AddOptions<MarkdownPipelineOptions>();
        services.ConfigureMarkdownPipeline((pipeline) =>
        {
            var extensions = _shellConfiguration.GetValue("Crest_Markdown:Extensions", DefaultMarkdownExtensions);
            pipeline.Configure(extensions);
        });

        services.AddScoped<IMarkdownService, DefaultMarkdownService>();
        services.AddResourceConfiguration<ResourceManagementOptionsConfiguration>();
    }
}
