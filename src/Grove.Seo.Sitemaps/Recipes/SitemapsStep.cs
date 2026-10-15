using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Grove.Json;
using Grove.Infrastructure.Recipes.Models;
using Grove.Infrastructure.Recipes.Services;
using Grove.Seo.Sitemaps.Models;
using Grove.Seo.Sitemaps.Services;

namespace Grove.Seo.Sitemaps.Recipes;

/// <summary>
/// This recipe step creates a set of sitemaps.
/// </summary>
public sealed class SitemapsStep : NamedRecipeStepHandler
{
    private readonly ISitemapManager _sitemapManager;
    private readonly DocumentJsonSerializerOptions _documentJsonSerializerOptions;

    public SitemapsStep(
        ISitemapManager sitemapManager,
        IOptions<DocumentJsonSerializerOptions> documentJsonSerializerOptions)
        : base("Sitemaps")
    {
        _sitemapManager = sitemapManager;
        _documentJsonSerializerOptions = documentJsonSerializerOptions.Value;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<SitemapStepModel>();

        foreach (var token in model.Data.Cast<JsonObject>())
        {
            var sitemap = token.ToObject<SitemapType>(_documentJsonSerializerOptions.SerializerOptions);
            await _sitemapManager.UpdateSitemapAsync(sitemap);
        }
    }

    public sealed class SitemapStepModel
    {
        public JsonArray Data { get; set; }
    }
}
