using System.Text.Json;
using System.Text.Json.Nodes;
using Grove.Tests.Apis.Context;
using Grove.Users;

namespace Grove.Tests.Modules.Grove.Users;

public class TwoFactorSendCodeEndpointTests
{
    [Theory]
    [InlineData(UserConstants.Features.EmailAuthenticator)]
    [InlineData(UserConstants.Features.SmsAuthenticator)]
    public async Task SendCode_RateLimitExceededReturnsTooManyRequests_Succeeds(string featureId)
    {
        // Arrange
        var context = await GetSiteContextAsync(featureId);

        // Act: the third request within the minute, whatever the first two answered.
        HttpResponseMessage response = null;

        for (var i = 0; i < 3; i++)
        {
            response = await context.Client.PostAsync("api/grove/auth/two-factor/send-code", System.Net.Http.Json.JsonContent.Create(new { provider = "Email" }), TestContext.Current.CancellationToken);
        }

        // Assert
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    private static async Task<SiteContext> GetSiteContextAsync(string featureId)
    {
        var context = new SiteContext();

        await context.InitializeAsync();

        var recipe = new JsonObject
        {
            ["steps"] = new JsonArray
            {
                new JsonObject
                {
                    { "name", "feature" },
                    { "enable", new JsonArray(UserConstants.Features.TwoFactorAuthentication, featureId, "Grove.RateLimits") },
                },
            },
        };

        await RecipeHelpers.RunRecipeAsync(context, recipe);

        return context;
    }
}
