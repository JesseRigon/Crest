using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System.Text.Json;
using Grove.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Grove.Api;

/// <summary>The host's <c>api/grove/admin-menu-layout</c> endpoints.</summary>
public sealed class AdminMenuLayoutApi : ApiContributor
{
    public override string Name => "AdminMenuLayout";

    public override string FeatureId => "Grove";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("admin-menu-layout");
        routes.MapPost("export", (HttpContext http, [FromQuery] string? file = null) => ApiHandlers.For<AdminMenuLayoutHandlers>(http).ExportAsync(file));
    }
}

public sealed class AdminMenuLayoutHandlers(
    IAuthorizationService authorizationService,
    IConfiguration configuration,
    IWebHostEnvironment environment,
    AdminMenuLayoutService layoutService) : ApiHandlers
{
    public const string DefaultFileName = "grove-admin-menu-layout.json";
    private const string ExportEnabledKey = "Grove:AdminMenuLayoutExport:Enabled";
    // Capital R: Grove's ApplicationRecipeHarvester scans ContentRoot/Recipes,
    // and that is case-sensitive on Linux. A lowercase name works on Windows/drvfs but
    // on ext4 it silently creates a SECOND directory the harvester never reads, so the
    // export looks like it succeeded while the tenant keeps loading the old layout.
    private const string RecipesDirectoryName = "Recipes";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public async Task<IResult> ExportAsync(string? file = null)
    {
        if (!environment.IsDevelopment() && !configuration.GetValue<bool>(ExportEnabledKey))
        {
            return NotFound();
        }

        if (!await authorizationService.AuthorizeAsync(User, Grove.Contents.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        var fileName = string.IsNullOrWhiteSpace(file) ? DefaultFileName : Path.GetFileName(file);
        if (!string.Equals(fileName, file ?? fileName, StringComparison.Ordinal))
        {
            return BadRequest("Only a file name is allowed.");
        }

        var recipesPath = Path.Combine(environment.ContentRootPath, RecipesDirectoryName);
        Directory.CreateDirectory(recipesPath);

        var outputPath = Path.Combine(recipesPath, fileName);
        var layout = await layoutService.ExportAsync();
        await System.IO.File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(layout, JsonOptions) + System.Environment.NewLine, HttpContext.RequestAborted);

        return Ok(new
        {
            file = fileName,
            path = outputPath,
            relativePath = Path.Combine(RecipesDirectoryName, fileName).Replace('\\', '/'),
            itemCount = layout.Items.Count,
            customItemCount = layout.CustomItems.Count,
            separatorCount = layout.Separators.Count,
        });
    }
}

/// <summary>The admin menu layout's change notifications (<c>api/grove/admin-menu-layout</c>, <see cref="AdminMenuLayoutHub"/>).</summary>
public sealed class AdminMenuLayoutHubApi : ApiContributor
{
    public override string Name => "AdminMenuLayoutHub";

    public override string FeatureId => "Grove";

    public override void Map(IEndpointRouteBuilder group) => group.MapHub<AdminMenuLayoutHub>("admin-menu-layout");
}
