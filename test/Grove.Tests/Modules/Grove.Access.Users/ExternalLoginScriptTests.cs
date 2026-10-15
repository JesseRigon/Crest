using System.Text.Json;
using System.Text.Json.Nodes;
using Grove.Tests.Apis.Context;
using Grove.Access.Users;
using Grove.Access.Users.Handlers;
using Grove.Access.Users.Indexes;
using Grove.Access.Users.Models;
using Grove.Access.Users.Services;

namespace Grove.Tests.Modules.Grove.Users;

/// <summary>The external login script handler's sync of claims, roles and properties onto a user.</summary>
public class ExternalLoginScriptTests
{
    [Fact]
    public async Task ExternalLoginSignIn_Default_Succeeds()
    {
        // Arrange
        var context = await GetSiteContextAsync(new RegistrationSettings(), true, true, true);

        // Act
        var model = new RegisterUserForm
        {
            UserName = "TestUserName",
            Email = "test@grove.com",
            Password = "test@OC!123",
        };

        await context.UsingTenantScopeAsync(async scope =>
        {
            var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
            var created = await userService.CreateUserAsync(new User { UserName = model.UserName, Email = model.Email, EmailConfirmed = true, IsEnabled = true }, model.Password, (_, _) => { });
            Assert.NotNull(created);
        });

        await context.UsingTenantScopeAsync(async scope =>
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IUser>>();

            var user = await userManager.FindByNameAsync(model.UserName) as User;

            var externalClaims = new List<SerializableClaim>();
            var userRoles = await userManager.GetRolesAsync(user);

            var context = new UpdateUserContext(user, "TestLoginProvider", externalClaims, user.Properties.DeepClone() as JsonObject)
            {
                UserClaims = user.UserClaims,
                UserRoles = userRoles,
            };

            context.UserProperties["Test"] = 111;
            Assert.NotEqual(user.Properties["Test"], context.UserProperties["Test"]);

            var scriptExternalLoginEventHandler = scope.ServiceProvider.GetServices<IExternalLoginEventHandler>()
                        .FirstOrDefault(x => x.GetType() == typeof(ScriptExternalLoginEventHandler)) as ScriptExternalLoginEventHandler;
            var loginSettings = new ExternalLoginSettings
            {
                UseScriptToSyncProperties = true,
                SyncPropertiesScript = """
                    if(!context.user.userClaims?.find(x=> x.claimType=="lastName" && claimValue=="Zhang")){
                        context.claimsToUpdate.push({claimType:"lastName",    claimValue:"Zhang"});
                    }
                    context.claimsToUpdate.push({claimType:"firstName",   claimValue:"Sam"});
                    context.claimsToUpdate.push({claimType:"displayName", claimValue:"Sam Zhang(CEO)"});
                    context.claimsToUpdate.push({claimType:"jobTitle",    claimValue:"CEO"});

                    if(!context.user.userRoles?.includes('Administrator')){
                        context.rolesToAdd.push("Administrator");
                    }

                    if(context.user.userProperties?.UserProfile?.UserProfile?.DisplayName?.Text!="Sam Zhang(CEO)")
                    {
                        context.propertiesToUpdate = {
                            "UserProfile": {
                                "UserProfile": {
                                    "DisplayName": {
                                        "Text": "Sam Zhang(CEO)"
                                    }
                                }
                            }
                        };
                    }
                    """,
            };
            await scriptExternalLoginEventHandler.UpdateUserInternalAsync(context, loginSettings);

            if (await userManager.UpdateUserPropertiesAsync(user, context))
            {
                await userManager.UpdateAsync(user);
            }

            var session = scope.ServiceProvider.GetRequiredService<YesSql.ISession>();
            var sam = await session.Query<User, UserByClaimIndex>()
                    .Where(claim => claim.ClaimType == "displayName" && claim.ClaimValue == "Sam Zhang(CEO)")
                    .FirstOrDefaultAsync();
            Assert.NotNull(sam);

            var claimsDict = sam.UserClaims.ToDictionary(claim => claim.ClaimType, claim => claim.ClaimValue);
            Assert.Equal("Sam", claimsDict["firstName"]);
            Assert.Equal("Zhang", claimsDict["lastName"]);
            Assert.Equal("CEO", claimsDict["jobTitle"]);
            Assert.Contains("Administrator", sam.RoleNames.FirstOrDefault());
            Assert.Equal("Sam Zhang(CEO)", sam.Properties.SelectNode("$.UserProfile.UserProfile.DisplayName.Text").ToString());

        });
        await context.UsingTenantScopeAsync(async scope =>
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IUser>>();

            var user = await userManager.FindByNameAsync(model.UserName) as User;

            var externalClaims = new List<SerializableClaim>();
            var userRoles = await userManager.GetRolesAsync(user);

            var updateContext = new UpdateUserContext(user, "TestLoginProvider", externalClaims, user.Properties.DeepClone() as JsonObject)
            {
                UserClaims = user.UserClaims,
                UserRoles = userRoles,
            };

            var scriptExternalLoginEventHandler = scope.ServiceProvider.GetServices<IExternalLoginEventHandler>()
                      .FirstOrDefault(x => x.GetType() == typeof(ScriptExternalLoginEventHandler)) as ScriptExternalLoginEventHandler;
            var loginSettings = new ExternalLoginSettings
            {
                UseScriptToSyncProperties = true,
                SyncPropertiesScript = """
                    context.claimsToUpdate.push({claimType:"displayName", claimValue:"Sam Zhang"});
                    context.claimsToUpdate.push({claimType:"firstName",   claimValue:"Sam"});
                    context.claimsToUpdate.push({claimType:"lastName",    claimValue:"Zhang"});
                    context.claimsToRemove.push({claimType:"jobTitle",    claimValue:"CEO"});
                    context.rolesToRemove.push("Administrator");
                    context.propertiesToUpdate = {
                        "UserProfile": {
                            "UserProfile": {
                                "DisplayName": {
                                    "Text": "Sam Zhang"
                                }
                            }
                        }
                    };
                    """,
            };
            await scriptExternalLoginEventHandler.UpdateUserInternalAsync(updateContext, loginSettings);

            if (await userManager.UpdateUserPropertiesAsync(user, updateContext))
            {
                await userManager.UpdateAsync(user);
            }

            var session = scope.ServiceProvider.GetRequiredService<YesSql.ISession>();
            var userFromDb = await session.Query<User, UserByClaimIndex>()
                         .Where(claim => claim.ClaimType == "displayName" && claim.ClaimValue == "Sam Zhang")
                         .FirstOrDefaultAsync();

            Assert.DoesNotContain("Administrator", userFromDb.RoleNames);
            Assert.DoesNotContain(userFromDb.UserClaims, x => x.ClaimType == "jobTitle" && x.ClaimValue == "CEO");
            Assert.Equal("Sam Zhang", userFromDb.Properties.SelectNode("$.UserProfile.UserProfile.DisplayName.Text").ToString());
        });
    }

    private static async Task<SiteContext> GetSiteContextAsync(RegistrationSettings settings, bool enableRegistrationFeature = true, bool requireUniqueEmail = true, bool enableExternalAuthentication = false, bool enableRateLimits = false)
    {
        var context = new SiteContext();

        await context.InitializeAsync();

        var recipeSteps = new JsonArray
        {
            new JsonObject
            {
                { "name", "settings" },
                { nameof(RegistrationSettings), JsonSerializer.SerializeToNode(settings)},
                { nameof(IdentitySettings), JsonSerializer.SerializeToNode(new IdentitySettings
                {
                    UserSettings = new IdentityUserSettings
                    {
                        RequireUniqueEmail = requireUniqueEmail,
                    },
                })},
            },
        };

        if (enableRegistrationFeature || enableExternalAuthentication)
        {
            var featureIds = new JsonArray();

            if (enableRegistrationFeature)
            {
                featureIds.Add(UserConstants.Features.UserRegistration);
            }

            if (enableExternalAuthentication)
            {
                featureIds.Add(UserConstants.Features.ExternalAuthentication);
            }

            if (enableRateLimits)
            {
                featureIds.Add("Grove.Security.RateLimits");
            }

            recipeSteps.Add(new JsonObject
            {
                { "name", "feature" },
                { "enable", featureIds },
            });
        }

        var recipe = new JsonObject
        {
            ["steps"] = recipeSteps,
        };

        await RecipeHelpers.RunRecipeAsync(context, recipe);

        return context;
    }
}
