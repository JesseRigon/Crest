using System.Text.Json.Nodes;
using Grove.Contents.Management;
using Grove.Infrastructure.Recipes.Models;
using Grove.Infrastructure.Recipes.Services;
using Grove.Access.Users.Models;
using YesSql;

namespace Grove.Access.Users.Recipes;

/// <summary>
/// This recipe step updates the custom user settings.
/// </summary>
public sealed class CustomUserSettingsStep : NamedRecipeStepHandler
{
    private readonly ISession _session;

    public CustomUserSettingsStep(ISession session)
        : base("custom-user-settings")
    {
        _session = session;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step;

        var customUserSettingsList = (JsonArray)model
            .AsEnumerable()
            .Where(p => p.Key != "name")
            .Select(p => p.Value)
            .FirstOrDefault();

        var allUsers = await _session.Query<User>().ListAsync();

        foreach (var userCustomUserSettings in customUserSettingsList.Cast<JsonObject>())
        {
            if (!userCustomUserSettings.TryGetPropertyValue("userId", out var jsonNode))
            {
                continue;
            }

            var userId = jsonNode.Value<string>();

            var user = allUsers.FirstOrDefault(u => u.UserId == userId);
            if (user is null)
            {
                continue;
            }

            if (!userCustomUserSettings.TryGetPropertyValue("user-custom-user-settings", out jsonNode) ||
                jsonNode is not JsonArray userSettings)
            {
                continue;
            }

            foreach (var userSetting in userSettings.Cast<JsonObject>())
            {
                var contentItem = userSetting.ToObject<ContentItem>();
                user.Properties[contentItem.ContentType] = userSetting.DeepClone();
            }

            await _session.SaveAsync(user);
        }
    }
}
