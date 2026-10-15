#nullable enable

using Grove.Security.Permissions;
using Grove.Validation;

namespace Grove.Settings;

/// <summary>
/// A settings group the generic settings form serves at <c>/Settings/{Id}</c>
/// (docs/MVC/parity.md › SettingsForm): its menu place, the permissions that admit a caller,
/// and the settings models it edits (<see cref="SettingsModel"/>; most groups have one, a group
/// several modules contribute to has one per module, each a section of the form). Registered
/// with <c>services.AddSettingsGroup&lt;T&gt;(...)</c>, a further model with
/// <c>services.AddSettingsModel&lt;T&gt;(groupId, ...)</c>; read from <see cref="SettingsGroupOptions"/>.
/// </summary>
public sealed class SettingsGroup
{
    /// <summary>The group id: the page route (<c>/Settings/{Id}</c>) and the API key.</summary>
    public required string Id { get; init; }

    /// <summary>The invariant caption: the menu item and the form's title.</summary>
    public required string Caption { get; set; }

    /// <summary>Permission names, any one of which admits the caller to the page and the API.</summary>
    public List<string> Permissions { get; } = [];

    /// <summary>The page registry entry id (the tenant's menu edits are keyed by it); the group id by default.</summary>
    public string? EntryId { get; set; }

    /// <summary>The menu entry the item sits under; <c>"settings"</c> by default.</summary>
    public string? Parent { get; set; } = "settings";

    /// <summary>The position among its siblings; by caption when absent.</summary>
    public string? Position { get; set; }

    /// <summary>The menu icon; none by default.</summary>
    public string? Icon { get; set; }

    /// <summary>A line under the title.</summary>
    public string? Description { get; set; }
}

/// <summary>
/// One settings model of a group: its type (whose public properties, DataAnnotations,
/// <c>Grove.Validation</c> rules and <see cref="SettingAttribute"/>s are the form's fields), where
/// it is loaded from and saved to (the site settings under the type's name by default), and the
/// rules and reactions that need services.
/// </summary>
public sealed class SettingsModel
{
    public required string GroupId { get; init; }

    /// <summary>The model's key in a group of several (its section's values and errors are under it): the camel-cased type name by default.</summary>
    public required string Key { get; set; }

    /// <summary>The section caption in a group of several; the group's caption by default.</summary>
    public string? Caption { get; set; }

    /// <summary>The order among the group's models.</summary>
    public int Order { get; set; }

    public required Type ModelType { get; init; }

    /// <summary>Saving a change reloads the tenant (the form says so before saving).</summary>
    public bool ReloadsTenant { get; set; }

    /// <summary>Loads the model (a new instance when nothing is stored).</summary>
    public required Func<IServiceProvider, CancellationToken, Task<object>> Load { get; set; }

    /// <summary>Stores the model.</summary>
    public required Func<IServiceProvider, object, CancellationToken, Task> Save { get; set; }

    /// <summary>Rules that need services, run after the model's own rules passed; errors are keyed by the field's wire name.</summary>
    public Func<IServiceProvider, object, ValidationErrors, CancellationToken, Task>? Validate { get; set; }

    /// <summary>Runs after a save that changed the model (an options refresh, a cache reset).</summary>
    public Func<IServiceProvider, object, CancellationToken, Task>? Saved { get; set; }
}

/// <summary>The registered settings groups, by id, and their models.</summary>
public sealed class SettingsGroupOptions
{
    public IDictionary<string, SettingsGroup> Groups { get; } = new Dictionary<string, SettingsGroup>(StringComparer.OrdinalIgnoreCase);

    public IList<SettingsModel> Models { get; } = [];

    /// <summary>The models of <paramref name="groupId"/>, in form order.</summary>
    public IReadOnlyList<SettingsModel> ModelsOf(string groupId) =>
        [.. Models.Where(model => string.Equals(model.GroupId, groupId, StringComparison.OrdinalIgnoreCase)).OrderBy(model => model.Order)];
}

/// <summary>Declares one settings model (<c>services.AddSettingsModel&lt;T&gt;(groupId, caption, model => ...)</c>).</summary>
public class SettingsModelBuilder<TModel>
    where TModel : class, new()
{
    internal SettingsModelBuilder(SettingsModel model) => Model = model;

    public SettingsModel Model { get; }

    /// <summary>Saving a change reloads the tenant.</summary>
    public SettingsModelBuilder<TModel> ReloadsTenant()
    {
        Model.ReloadsTenant = true;
        return this;
    }

    /// <summary>The order among the group's models.</summary>
    public SettingsModelBuilder<TModel> At(int order)
    {
        Model.Order = order;
        return this;
    }

    /// <summary>Stores the model somewhere else than the site settings (a document, a store of its own).</summary>
    public SettingsModelBuilder<TModel> StoredBy(
        Func<IServiceProvider, CancellationToken, Task<TModel>> load,
        Func<IServiceProvider, TModel, CancellationToken, Task> save)
    {
        Model.Load = async (services, cancellationToken) => await load(services, cancellationToken);
        Model.Save = (services, model, cancellationToken) => save(services, (TModel)model, cancellationToken);
        return this;
    }

    /// <summary>Rules that need services; add errors under the property name (camel-cased to the wire name).</summary>
    public SettingsModelBuilder<TModel> ValidatedBy(Func<IServiceProvider, TModel, ValidationErrors, CancellationToken, Task> validate)
    {
        Model.Validate = (services, model, errors, cancellationToken) => validate(services, (TModel)model, errors, cancellationToken);
        return this;
    }

    /// <summary>Runs after a save that changed the model.</summary>
    public SettingsModelBuilder<TModel> OnSaved(Func<IServiceProvider, TModel, CancellationToken, Task> saved)
    {
        Model.Saved = (services, model, cancellationToken) => saved(services, (TModel)model, cancellationToken);
        return this;
    }
}

/// <summary>Declares one settings group and its first model (<c>services.AddSettingsGroup&lt;T&gt;(id, caption, permission, group => ...)</c>).</summary>
public sealed class SettingsGroupBuilder<TModel> : SettingsModelBuilder<TModel>
    where TModel : class, new()
{
    internal SettingsGroupBuilder(SettingsGroup group, SettingsModel model)
        : base(model) => Group = group;

    public SettingsGroup Group { get; }

    public SettingsGroupBuilder<TModel> InMenu(string? parent, string? position = null, string? icon = null, string? entryId = null)
    {
        Group.EntryId = entryId ?? Group.EntryId;
        Group.Parent = parent;
        Group.Position = position;
        Group.Icon = icon;
        return this;
    }

    public SettingsGroupBuilder<TModel> Describe(string description)
    {
        Group.Description = description;
        return this;
    }

    /// <inheritdoc cref="SettingsModelBuilder{TModel}.ReloadsTenant"/>
    public new SettingsGroupBuilder<TModel> ReloadsTenant()
    {
        base.ReloadsTenant();
        return this;
    }

    /// <inheritdoc cref="SettingsModelBuilder{TModel}.StoredBy"/>
    public new SettingsGroupBuilder<TModel> StoredBy(
        Func<IServiceProvider, CancellationToken, Task<TModel>> load,
        Func<IServiceProvider, TModel, CancellationToken, Task> save)
    {
        base.StoredBy(load, save);
        return this;
    }

    /// <inheritdoc cref="SettingsModelBuilder{TModel}.ValidatedBy"/>
    public new SettingsGroupBuilder<TModel> ValidatedBy(Func<IServiceProvider, TModel, ValidationErrors, CancellationToken, Task> validate)
    {
        base.ValidatedBy(validate);
        return this;
    }

    /// <inheritdoc cref="SettingsModelBuilder{TModel}.OnSaved"/>
    public new SettingsGroupBuilder<TModel> OnSaved(Func<IServiceProvider, TModel, CancellationToken, Task> saved)
    {
        base.OnSaved(saved);
        return this;
    }

    /// <summary>Another permission that admits the caller.</summary>
    public SettingsGroupBuilder<TModel> AlsoAdmits(Permission permission)
    {
        if (!Group.Permissions.Contains(permission.Name, StringComparer.Ordinal))
        {
            Group.Permissions.Add(permission.Name);
        }

        return this;
    }
}
