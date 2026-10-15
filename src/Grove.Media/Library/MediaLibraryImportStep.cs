#nullable enable
using System.Text.Json.Nodes;
using Grove.BackgroundJobs;
using Grove.Infrastructure.Recipes.Models;
using Grove.Infrastructure.Recipes.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Media.Library;

/// <summary>
/// The <c>MediaLibraryImport</c> recipe step: runs the media library import (ruling F1) again, or
/// for the first time with a chosen scanning mode. Safe to repeat: what is already in the drive is
/// kept, and fields already holding ids are left alone.
/// <code>{ "name": "MediaLibraryImport", "TrustExisting": false }</code>
/// Within a request the import runs after the response, in its own scope.
/// </summary>
public sealed class MediaLibraryImportStep(IHttpContextAccessor httpContextAccessor, IServiceProvider services) : NamedRecipeStepHandler(Name)
{
    public const string Name = "MediaLibraryImport";

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<MediaLibraryImportStepModel>();
        var trustExisting = model?.TrustExisting;

        if (httpContextAccessor.HttpContext is null)
        {
            await services.GetRequiredService<MediaLibraryImporter>().ImportAsync(trustExisting);
            return;
        }

        await HttpBackgroundJob.ExecuteAfterEndOfRequestAsync("media-library-import", trustExisting, (scope, trust) =>
            scope.ServiceProvider.GetRequiredService<MediaLibraryImporter>().ImportAsync(trust));
    }
}

public sealed class MediaLibraryImportStepModel
{
    /// <summary>Null takes the configured default (<c>Grove_Media:Library:TrustExistingFiles</c>).</summary>
    public bool? TrustExisting { get; set; }
}
