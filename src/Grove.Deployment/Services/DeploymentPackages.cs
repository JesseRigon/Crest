using System.IO.Compression;
using Grove.Deployment.Steps;
using Grove.FileStorage;
using Grove.Recipes.Models;
using Microsoft.Extensions.FileProviders;

namespace Grove.Deployment.Services;

/// <summary>
/// Builds a plan's package and imports one: the work behind the admin's export and import
/// screens, shared by the MVC controllers and the Grove admin API.
/// </summary>
public static class DeploymentPackages
{
    /// <summary>
    /// Runs the plan into a zip under the tenant's temporary root and returns its path; the
    /// caller deletes it. A <see cref="RecipeFileDeploymentStep"/> in the plan names the recipe.
    /// </summary>
    public static async Task<string> CreateArchiveAsync(IDeploymentManager deploymentManager, ITempDirectoryProvider tempDirectoryProvider, DeploymentPlan deploymentPlan)
    {
        string archiveFileName;

        using (var fileBuilder = new TemporaryFileBuilder(tempDirectoryProvider.GetRootDirectory()))
        {
            archiveFileName = fileBuilder.Folder + ".zip";

            var recipeDescriptor = new RecipeDescriptor();

            if (deploymentPlan.DeploymentSteps.FirstOrDefault(step => step.Name == nameof(RecipeFileDeploymentStep)) is RecipeFileDeploymentStep recipeFile)
            {
                recipeDescriptor.Name = recipeFile.RecipeName;
                recipeDescriptor.DisplayName = recipeFile.DisplayName;
                recipeDescriptor.Description = recipeFile.Description;
                recipeDescriptor.Author = recipeFile.Author;
                recipeDescriptor.WebSite = recipeFile.WebSite;
                recipeDescriptor.Version = recipeFile.Version;
                recipeDescriptor.IsSetupRecipe = recipeFile.IsSetupRecipe;
                recipeDescriptor.Categories = (recipeFile.Categories ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
                recipeDescriptor.Tags = (recipeFile.Tags ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
            }

            var deploymentPlanResult = new DeploymentPlanResult(fileBuilder, recipeDescriptor);
            await deploymentManager.ExecuteDeploymentPlanAsync(deploymentPlan, deploymentPlanResult);
            ZipFile.CreateFromDirectory(fileBuilder.Folder, archiveFileName);
        }

        return archiveFileName;
    }

    /// <summary>
    /// Imports an uploaded package: a zip, or a single recipe (.json). The content must already
    /// have passed the upload pipeline (<see cref="FileCreationService"/>).
    /// </summary>
    /// <exception cref="NotSupportedException">The file is neither a zip nor a json file.</exception>
    public static async Task ImportPackageAsync(IDeploymentManager deploymentManager, ITempDirectoryProvider tempDirectoryProvider, string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        var isZip = fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

        if (!isZip && !fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("Only zip or json files are supported.");
        }

        var tempArchiveName = tempDirectoryProvider.GetTempFileName(Path.GetExtension(fileName));
        var tempArchiveFolder = tempDirectoryProvider.GetTempFileName();

        try
        {
            await using (var stream = new FileStream(tempArchiveName, FileMode.Create))
            {
                await content.CopyToAsync(stream, cancellationToken);
            }

            if (isZip)
            {
                ZipFile.ExtractToDirectory(tempArchiveName, tempArchiveFolder);
            }
            else
            {
                Directory.CreateDirectory(tempArchiveFolder);
                File.Move(tempArchiveName, Path.Combine(tempArchiveFolder, "Recipe.json"));
            }

            await deploymentManager.ImportDeploymentPackageAsync(new PhysicalFileProvider(tempArchiveFolder));
        }
        finally
        {
            if (File.Exists(tempArchiveName))
            {
                File.Delete(tempArchiveName);
            }

            if (Directory.Exists(tempArchiveFolder))
            {
                Directory.Delete(tempArchiveFolder, true);
            }
        }
    }

    /// <summary>Imports a recipe given as JSON text.</summary>
    public static async Task ImportJsonAsync(IDeploymentManager deploymentManager, ITempDirectoryProvider tempDirectoryProvider, string json)
    {
        var tempArchiveFolder = tempDirectoryProvider.CreateTempSubdirectory();

        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempArchiveFolder, "Recipe.json"), json);
            await deploymentManager.ImportDeploymentPackageAsync(new PhysicalFileProvider(tempArchiveFolder));
        }
        finally
        {
            if (Directory.Exists(tempArchiveFolder))
            {
                Directory.Delete(tempArchiveFolder, true);
            }
        }
    }
}
