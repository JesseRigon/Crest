using Grove.Data.Protocols.GraphQL.Client;
using Grove.BackgroundJobs;
using Grove.BackgroundTasks;
using Grove.Contents.Management;
using Grove.Environment.Shell;
using Grove.Environment.Shell.Scope;
using Grove.Infrastructure.Recipes.Services;

namespace Grove.Tests.Apis.Context;

public class SiteContext : IDisposable
{
    private const int DeferredTasksTimeoutSeconds = 60;
    private const int HttpBackgroundJobsTimeoutSeconds = 90;
    private const int WaitDelayMilliseconds = 10;

    private static readonly TablePrefixGenerator s_tablePrefixGenerator = new();
    public static PlatformTestFixture<SiteStartup> Site { get; }
    public static IShellHost ShellHost { get; private set; }
    public static IShellSettingsManager ShellSettingsManager { get; private set; }
    public static IHttpContextAccessor HttpContextAccessor { get; }
    public static HttpClient DefaultTenantClient { get; }

    public string RecipeName { get; set; } = TestRecipes.Blog;
    public string DatabaseProvider { get; set; } = "Sqlite";
    public string ConnectionString { get; set; }
    public PermissionsContext PermissionsContext { get; set; }

    public HttpClient Client { get; private set; }
    public string TenantName { get; private set; }
    public PlatformGraphQLClient GraphQLClient { get; private set; }

    static SiteContext()
    {
        Site = new PlatformTestFixture<SiteStartup>();
        ShellHost = Site.Services.GetRequiredService<IShellHost>();
        ShellSettingsManager = Site.Services.GetRequiredService<IShellSettingsManager>();
        HttpContextAccessor = Site.Services.GetRequiredService<IHttpContextAccessor>();
        DefaultTenantClient = Site.CreateDefaultClient();
    }

    public virtual async Task InitializeAsync()
    {
        var tenantName = Guid.NewGuid().ToString("n");
        var tablePrefix = await s_tablePrefixGenerator.GeneratePrefixAsync();

        var createModel = new global::Grove.Infrastructure.Tenants.Models.TenantApiModel
        {
            DatabaseProvider = DatabaseProvider,
            TablePrefix = tablePrefix,
            ConnectionString = ConnectionString,
            RecipeName = RecipeName,
            Name = tenantName,
            RequestUrlPrefix = tenantName,
            Schema = null,
        };

        var createResult = await DefaultTenantClient.PostAsJsonAsync("api/tenants/create", createModel);
        if (!createResult.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Tenant create with recipe '{RecipeName}' failed: {(int)createResult.StatusCode} {await createResult.Content.ReadAsStringAsync()}");
        }

        var content = await createResult.Content.ReadAsStringAsync();

        var url = new Uri(content.Trim('"'));
        url = new Uri(url.Scheme + "://" + url.Authority + url.LocalPath + "/");

        var setupModel = new global::Grove.Infrastructure.Tenants.ViewModels.SetupApiViewModel
        {
            SiteName = "Test Site",
            DatabaseProvider = DatabaseProvider,
            TablePrefix = tablePrefix,
            ConnectionString = ConnectionString,
            RecipeName = RecipeName,
            UserName = "admin",
            Password = "Password01_",
            Name = tenantName,
            Email = "Nick@Grove",
        };

        var setupResult = await DefaultTenantClient.PostAsJsonAsync("api/tenants/setup", setupModel);
        if (!setupResult.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Tenant setup with recipe '{RecipeName}' failed: {(int)setupResult.StatusCode} {await setupResult.Content.ReadAsStringAsync()}");
        }

        lock (Site)
        {
            Client = Site.CreateDefaultClient(url);
            TenantName = tenantName;
        }

        if (PermissionsContext != null)
        {
            var permissionContextKey = Guid.NewGuid().ToString();
            SiteStartup.PermissionsContexts.TryAdd(permissionContextKey, PermissionsContext);
            Client.DefaultRequestHeaders.Add("PermissionsContext", permissionContextKey);
        }

        GraphQLClient = new PlatformGraphQLClient(Client);
    }

    public async Task UsingTenantScopeAsync(Func<ShellScope, Task> execute, bool activateShell = true)
    {
        // Ensure that 'HttpContext' is not null before using a 'ShellScope'.
        var shellScope = await ShellHost.GetScopeAsync(TenantName);
        HttpContextAccessor.HttpContext = shellScope.ShellContext.CreateHttpContext();
        await shellScope.UsingAsync(execute, activateShell);

        HttpContextAccessor.HttpContext = null;
    }

    // Waits up to 60 seconds for all outstanding deferred tasks to complete by making sure no shell scope is
    // currently executing.
    public Task WaitForDeferredTasksAsync(CancellationToken cancellationToken)
    {
        return UsingTenantScopeAsync(async scope =>
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(DeferredTasksTimeoutSeconds));

            // If there is only one active scope (the current one), it means that all deferred tasks have completed.
            while (!cts.Token.IsCancellationRequested &&
                    scope.ShellContext.ActiveScopes > 1)
            {
                await Task.Delay(WaitDelayMilliseconds, cancellationToken);
            }

            if (cts.IsCancellationRequested)
            {
                throw new TimeoutException("Not all deferred tasks have completed within the expected time frame.");
            }
        });
    }

    // Waits up to 90 seconds for all outstanding HTTP background jobs.
    // This also handles nested jobs where completing one job may spawn another.
    public Task WaitForHttpBackgroundJobsAsync(CancellationToken cancellationToken)
    {
        return UsingTenantScopeAsync(async scope =>
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(HttpBackgroundJobsTimeoutSeconds));

            while (!cts.Token.IsCancellationRequested &&
                    HttpBackgroundJob.ActiveJobsCount > 0)
            {
                await Task.Delay(WaitDelayMilliseconds, cancellationToken);
            }

            if (cts.IsCancellationRequested)
            {
                throw new TimeoutException("Not all HTTP background jobs have completed within the expected time frame.");
            }
        });
    }

    public Task RunRecipeAsync(string recipeName, string recipePath)
    {
        return UsingTenantScopeAsync(async scope =>
        {
            var shellFeaturesManager = scope.ServiceProvider.GetRequiredService<IShellFeaturesManager>();
            var recipeHarvesters = scope.ServiceProvider.GetRequiredService<IEnumerable<IRecipeHarvester>>();
            var recipeExecutor = scope.ServiceProvider.GetRequiredService<IRecipeExecutor>();

            var recipeCollections = await Task.WhenAll(
                recipeHarvesters.Select(recipe => recipe.HarvestRecipesAsync()));

            var recipes = recipeCollections.SelectMany(recipeCollection => recipeCollection);
            var recipe = recipes
                .FirstOrDefault(recipe => recipe.RecipeFileInfo.Name == recipeName && recipe.BasePath == recipePath);

            var executionId = Guid.NewGuid().ToString("n");

            await recipeExecutor.ExecuteAsync(
                executionId,
                recipe,
                new Dictionary<string, object>(),
                CancellationToken.None);
        });
    }

    public async Task<string> CreateContentItem(string contentType, Action<ContentItem> func, bool draft = false)
    {
        // Never generate a fake ContentItemId here as it should be created by the ContentManager.NewAsync() method.
        // The content manager's sequence calls the event handlers (ActivatingAsync, ActivatedAsync,
        // InitializingAsync, InitializedAsync, ...).
        var contentItem = new ContentItem
        {
            ContentType = contentType,
        };

        func(contentItem);

        return (await SaveContentItemAsync(contentItem, draft)).ContentItemId;
    }

    /// <summary>The published version of a content item, read through the tenant's content manager.</summary>
    public async Task<ContentItem> GetContentItemAsync(string contentItemId)
    {
        ContentItem contentItem = null;
        await UsingTenantScopeAsync(async scope =>
        {
            contentItem = await scope.ServiceProvider.GetRequiredService<IContentManager>().GetAsync(contentItemId);
        });

        return contentItem;
    }

    /// <summary>
    /// Creates a content item from a model, or merges the model into the item's draft when it
    /// exists, then publishes it or saves it as a draft (what the retired <c>api/content</c> upsert
    /// did, in-process). Returns the saved item.
    /// </summary>
    public async Task<ContentItem> SaveContentItemAsync(ContentItem model, bool draft = false)
    {
        ContentItem contentItem = null;
        await UsingTenantScopeAsync(async scope =>
        {
            var contentManager = scope.ServiceProvider.GetRequiredService<IContentManager>();
            contentItem = model.ContentItemId is null ? null : await contentManager.GetAsync(model.ContentItemId, VersionOptions.DraftRequired);

            if (contentItem is null)
            {
                contentItem = await contentManager.NewAsync(model.ContentType);
                contentItem.Merge(model);
                await contentManager.CreateAsync(contentItem, VersionOptions.Draft);
            }
            else
            {
                contentItem.Merge(model, new System.Text.Json.Settings.JsonMergeSettings { MergeArrayHandling = System.Text.Json.Settings.MergeArrayHandling.Replace });
                await contentManager.UpdateAsync(contentItem);
            }

            if (draft)
            {
                await contentManager.SaveDraftAsync(contentItem);
            }
            else
            {
                await contentManager.PublishAsync(contentItem);
            }
        });

        return contentItem;
    }

    public Task DeleteContentItem(string contentItemId)
    {
        return UsingTenantScopeAsync(async scope =>
        {
            var contentManager = scope.ServiceProvider.GetRequiredService<IContentManager>();
            var contentItem = await contentManager.GetAsync(contentItemId);
            if (contentItem is not null)
            {
                await contentManager.RemoveAsync(contentItem);
            }
        });
    }

#pragma warning disable CA1816 // Change SiteContext.Dispose() to call GC.SuppressFinalize(object). This will prevent derived types that introduce a finalizer from needing to re-implement 'IDisposable' to call it.
    public void Dispose()
#pragma warning restore CA1816
    {
        Client?.Dispose();
    }
}

public static class SiteContextExtensions
{
    public static T WithDatabaseProvider<T>(this T siteContext, string databaseProvider) where T : SiteContext
    {
        siteContext.DatabaseProvider = databaseProvider;
        return siteContext;
    }

    public static T WithConnectionString<T>(this T siteContext, string connectionString) where T : SiteContext
    {
        siteContext.ConnectionString = connectionString;
        return siteContext;
    }

    public static T WithPermissionsContext<T>(this T siteContext, PermissionsContext permissionsContext) where T : SiteContext
    {
        siteContext.PermissionsContext = permissionsContext;
        return siteContext;
    }

    public static T WithRecipe<T>(this T siteContext, string recipeName) where T : SiteContext
    {
        siteContext.RecipeName = recipeName;
        return siteContext;
    }
}
