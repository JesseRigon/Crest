using Crest.ContentManagement;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.ContentFields.Relationships;

/// <summary>
/// A rule the owner of a content picker field registers for it: hierarchies built from
/// pickers (parent organization, parent location, parent account) are built in many
/// ways, so the picker cannot know how to walk every graph; the owner can. Runs on every
/// save path (it is called from the content manager's save guard), after the picker's own
/// server-side checks of the allowed target types (docs/content-items.md › Picker
/// relationship handlers).
/// </summary>
public interface IContentPickerRelationshipHandler
{
    Task ValidateAsync(ContentPickerRelationshipContext context);
}

/// <summary>One picker field value about to be saved.</summary>
public sealed class ContentPickerRelationshipContext
{
    private readonly List<string> _errors;

    public ContentPickerRelationshipContext(ContentItem root, ContentItem item, string partName, string fieldName, IReadOnlyList<string> selectedIds, List<string> errors)
    {
        Root = root;
        Item = item;
        PartName = partName;
        FieldName = fieldName;
        SelectedIds = selectedIds;
        _errors = errors;
    }

    /// <summary>The item being saved.</summary>
    public ContentItem Root { get; }

    /// <summary>The item holding the field: <see cref="Root"/>, or an item contained in it (a bag element).</summary>
    public ContentItem Item { get; }

    public string PartName { get; }

    public string FieldName { get; }

    public IReadOnlyList<string> SelectedIds { get; }

    public void Fail(string error) => _errors.Add(error);
}

/// <summary>Which handler owns which picker field, keyed by (part name, field name).</summary>
public sealed class ContentPickerRelationshipOptions
{
    private readonly Dictionary<(string Part, string Field), List<Type>> _handlers = [];

    public void Add(string partName, string fieldName, Type handlerType)
    {
        if (!_handlers.TryGetValue((partName, fieldName), out var list))
        {
            _handlers[(partName, fieldName)] = list = [];
        }

        if (!list.Contains(handlerType))
        {
            list.Add(handlerType);
        }
    }

    public IReadOnlyList<Type> For(string partName, string fieldName) =>
        _handlers.TryGetValue((partName, fieldName), out var list) ? list : [];
}

public static class ContentPickerRelationshipServiceCollectionExtensions
{
    /// <summary>Registers <typeparamref name="THandler"/> as the relationship rule of the
    /// picker field <paramref name="fieldName"/> on the part <paramref name="partName"/>.</summary>
    public static IServiceCollection AddContentPickerRelationshipHandler<THandler>(this IServiceCollection services, string partName, string fieldName)
        where THandler : class, IContentPickerRelationshipHandler
    {
        services.AddScoped<THandler>();
        services.Configure<ContentPickerRelationshipOptions>(options => options.Add(partName, fieldName, typeof(THandler)));
        return services;
    }
}
