using System.Text.Json.Nodes;
using Grove.Deployment;
using Grove.Access.OpenId.Deployment;
using Grove.Access.OpenId.Recipes;
using Grove.Access.OpenId.Services;
using Grove.Access.OpenId.Settings;
using Grove.Infrastructure.Recipes.Models;
using Grove.Tests.Stubs;

namespace Grove.Tests.Modules.Grove.OpenId;

public class OpenIdServerDeploymentSourceTests
{
    [Fact]
    public async Task ServerDeploymentSourceIsReadableByRecipe_Default_Succeeds()
    {
        // Arrange
        var settings = CreateSettings("https://deploy.localhost", OpenIdServerSettings.TokenFormat.JsonWebToken, true);
        var openIdServerService = new Mock<IOpenIdServerService>();
        openIdServerService
            .Setup(m => m.GetSettingsAsync())
            .ReturnsAsync(settings);

        openIdServerService
            .Setup(m => m.LoadSettingsAsync())
            .ReturnsAsync(settings);

        var fileBuilder = new MemoryFileBuilder();
        var descriptor = new RecipeDescriptor();
        var result = new DeploymentPlanResult(fileBuilder, descriptor);
        var deploymentSource = new OpenIdServerDeploymentSource(openIdServerService.Object);

        // Act
        await deploymentSource.ProcessDeploymentStepAsync(new OpenIdServerDeploymentStep(), result);
        await result.FinalizeAsync();

        // Assert
        await ExecuteRecipeAsync(openIdServerService.Object, fileBuilder, descriptor);

        var updatedSettings = await openIdServerService.Object.LoadSettingsAsync();
        Assert.Equal(settings, updatedSettings);
    }

    private static OpenIdServerSettings CreateSettings(string authority, OpenIdServerSettings.TokenFormat tokenFormat, bool initializeAllProperties)
    {
        var result = new OpenIdServerSettings
        {
            Authority = new Uri(authority),
            AccessTokenFormat = tokenFormat,
        };

        if (initializeAllProperties)
        {
            result.TokenEndpointPath = "/connect/token";
            result.AuthorizationEndpointPath = "/connect/authorize";
            result.LogoutEndpointPath = "/connect/logout";
            result.UserinfoEndpointPath = "/connect/userinfo";
            result.IntrospectionEndpointPath = "/connect/introspect";
            result.PushedAuthorizationEndpointPath = "/connect/par";
            result.RevocationEndpointPath = "/connect/revoke";

            result.EncryptionCertificateStoreLocation = StoreLocation.LocalMachine;
            result.EncryptionCertificateStoreName = StoreName.My;
            result.EncryptionCertificateThumbprint = Guid.NewGuid().ToString();

            result.SigningCertificateStoreLocation = StoreLocation.LocalMachine;
            result.SigningCertificateStoreName = StoreName.My;
            result.SigningCertificateThumbprint = Guid.NewGuid().ToString();

            result.AllowAuthorizationCodeFlow = true;
            result.AllowClientCredentialsFlow = true;
            result.AllowHybridFlow = true;
            result.AllowImplicitFlow = true;
            result.AllowPasswordFlow = true;
            result.AllowRefreshTokenFlow = true;

            result.DisableAccessTokenEncryption = true;
            result.DisableRollingRefreshTokens = true;
            result.UseReferenceAccessTokens = true;
            result.RequireProofKeyForCodeExchange = true;
            result.RequirePushedAuthorizationRequests = true;
        }

        return result;
    }

    private static async Task ExecuteRecipeAsync(
        IOpenIdServerService openIdServerService,
        MemoryFileBuilder fileBuilder,
        RecipeDescriptor recipeDescriptor)
    {
        var recipeFile = "Recipe.json";
        var content = fileBuilder.GetFileContents(recipeFile, Encoding.UTF8);
        var deploy = JsonNode.Parse(content);

        var recipeContext = new RecipeExecutionContext
        {
            RecipeDescriptor = recipeDescriptor,
            Name = deploy["steps"][0].Value<string>("name"),
            Step = (JsonObject)deploy["steps"][0],
        };

        var recipeStep = new OpenIdServerSettingsStep(openIdServerService);
        await recipeStep.ExecuteAsync(recipeContext);
    }
}
