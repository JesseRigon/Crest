#nullable enable

using Grove.Entities;
using Grove.Security.Permissions;
using Grove.Settings;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers settings groups and the permissions that grant access to them.
/// </summary>
public static class SettingsServiceCollectionExtensions
{
    /// <summary>
    /// Registers a permission that grants access to a site settings group.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="groupId">The site settings group identifier.</param>
    /// <param name="permission">The permission that grants access to the group.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddSiteSettingsPermission(
        this IServiceCollection services,
        string groupId,
        Permission permission)
    {
        ArgumentException.ThrowIfNullOrEmpty(groupId);
        ArgumentNullException.ThrowIfNull(permission);

        services.Configure<SiteSettingsPermissionOptions>(options =>
        {
            if (!options.GroupPermissions.TryGetValue(groupId, out var permissions))
            {
                permissions = [];
                options.GroupPermissions[groupId] = permissions;
            }

            if (!permissions.Any(existing => existing.Name.Equals(permission.Name, StringComparison.OrdinalIgnoreCase)))
            {
                permissions.Add(permission);
            }
        });

        return services;
    }

    /// <summary>
    /// Registers a settings group the generic settings form serves at <c>/Settings/{groupId}</c>
    /// (docs/MVC/parity.md › SettingsForm) with its first model: the form is derived from
    /// <typeparamref name="TModel"/>, stored in the site settings under the type's name unless
    /// <paramref name="configure"/> gives it a store of its own, and admitted by <paramref name="permission"/>.
    /// </summary>
    public static IServiceCollection AddSettingsGroup<TModel>(
        this IServiceCollection services,
        string groupId,
        string caption,
        Permission permission,
        Action<SettingsGroupBuilder<TModel>>? configure = null)
        where TModel : class, new()
    {
        ArgumentException.ThrowIfNullOrEmpty(groupId);
        ArgumentException.ThrowIfNullOrEmpty(caption);
        ArgumentNullException.ThrowIfNull(permission);

        var group = new SettingsGroup { Id = groupId, Caption = caption };
        group.Permissions.Add(permission.Name);
        var model = CreateModel<TModel>(groupId);
        configure?.Invoke(new SettingsGroupBuilder<TModel>(group, model));

        services.Configure<SettingsGroupOptions>(options =>
        {
            options.Groups[groupId] = group;
            options.Models.Add(model);
        });
        services.AddSiteSettingsPermission(groupId, permission);

        return services;
    }

    /// <summary>
    /// Adds a settings model to a group another registration declares (a provider's section of the
    /// SMS settings, the sitemaps' rule of the robots settings): a section of that group's form,
    /// captioned <paramref name="caption"/>.
    /// </summary>
    public static IServiceCollection AddSettingsModel<TModel>(
        this IServiceCollection services,
        string groupId,
        string caption,
        Action<SettingsModelBuilder<TModel>>? configure = null)
        where TModel : class, new()
    {
        ArgumentException.ThrowIfNullOrEmpty(groupId);
        ArgumentException.ThrowIfNullOrEmpty(caption);

        var model = CreateModel<TModel>(groupId);
        model.Caption = caption;
        model.Order = 100;
        configure?.Invoke(new SettingsModelBuilder<TModel>(model));
        services.Configure<SettingsGroupOptions>(options => options.Models.Add(model));

        return services;
    }

    private static SettingsModel CreateModel<TModel>(string groupId)
        where TModel : class, new() => new()
        {
            GroupId = groupId,
            Key = System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(typeof(TModel).Name),
            ModelType = typeof(TModel),
            Load = async (provider, _) => await provider.GetRequiredService<ISiteService>().GetSettingsAsync<TModel>(),
            Save = async (provider, value, _) =>
            {
                var siteService = provider.GetRequiredService<ISiteService>();
                var site = await siteService.LoadSiteSettingsAsync();
                site.Put((TModel)value);
                await siteService.UpdateSiteSettingsAsync(site);
            },
        };
}
