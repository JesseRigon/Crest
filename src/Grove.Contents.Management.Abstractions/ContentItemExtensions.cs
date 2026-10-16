using System.Text.Json.Nodes;
using System.Text.Json.Settings;

namespace Grove.Contents.Management;

public static class ContentItemExtensions
{
    /// <summary>
    /// Tries to get a field set by its type.
    /// </summary>
    /// <typeparam name="TFieldSet">The type of the field set.</typeparam>
    /// <param name="contentItem">The <see cref="ContentItem"/>.</param>
    /// <param name="part">The <see cref="ContentFieldSet"/> if one existed.</param>
    /// <returns>true if a part found, otherwise false.</returns>
    public static bool TryGet<TFieldSet>(this ContentItem contentItem, out TFieldSet part) where TFieldSet : ContentFieldSet
        => contentItem.TryGet(ContentFieldSetName.Of<TFieldSet>(), out part);

    /// <summary>
    /// Tries to get a field set by its type.
    /// </summary>
    /// <typeparam name="TFieldSet">The type of the field set.</typeparam>
    /// <param name="contentItem">The <see cref="ContentItem"/>.</param>
    /// <param name="name">The name of the field set.</param>
    /// <param name="part">The <see cref="ContentFieldSet"/> if one existed.</param>
    /// <returns>true if a part found, otherwise false.</returns>
    public static bool TryGet<TFieldSet>(this ContentItem contentItem, string name, out TFieldSet part) where TFieldSet : ContentFieldSet
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        try
        {
            part = contentItem.Get<TFieldSet>(name);
        }
        catch
        {
            part = null;
        }

        return part != null;
    }

    /// <summary>
    /// Tries to get a field set by its type.
    /// </summary>
    /// <param name="contentItem">The <see cref="ContentItem"/>.</param>
    /// <param name="contentElementType">The type of the field set.</param>
    /// <param name="name">The name of the field set.</param>
    /// <param name="part">The <see cref="ContentFieldSet"/> if one existed.</param>
    /// <returns>true if a part found, otherwise false.</returns>
    public static bool TryGet(this ContentItem contentItem, Type contentElementType, string name, out ContentElement part)
    {
        try
        {
            part = contentItem.Get(contentElementType, name);
        }
        catch
        {
            part = null;
        }

        return part != null;
    }

    /// <summary>
    ///  Gets a field set by its type. ⚠️ Not recommended for use. Prefer <see cref="GetOrCreate{TFieldSet}(ContentItem)"/> and <see cref="TryGet{TFieldSet}(ContentItem, out TFieldSet)"/> instead. 
    /// </summary>
    /// <param name="contentItem">The <see cref="ContentItem"/>.</param>
    /// <typeparam name="TFieldSet">The type of the field set.</typeparam>
    /// <returns>The field set or. <code>null</code> if it doesn't exist.</returns>
    [Obsolete("Use TryGet<TFieldSet>, GetOrCreate<TFieldSet> or Get<TFieldSet> instead.")]
    public static TFieldSet As<TFieldSet>(this ContentItem contentItem) where TFieldSet : ContentFieldSet
        => contentItem.Get<TFieldSet>(ContentFieldSetName.Of<TFieldSet>());

    /// <summary>
    /// Gets a field set by its type or create a new one.
    /// </summary>
    /// <param name="contentItem">The <see cref="ContentItem"/>.</param>
    /// <typeparam name="TFieldSet">The type of the field set.</typeparam>
    /// <returns>The field set instance or a new one if it doesn't exist.</returns>
    public static TFieldSet GetOrCreate<TFieldSet>(this ContentItem contentItem) where TFieldSet : ContentFieldSet, new()
        => contentItem.GetOrCreate<TFieldSet>(ContentFieldSetName.Of<TFieldSet>());

    /// <summary>
    /// Removes a field set by its type.
    /// </summary>
    /// <param name="contentItem">The <see cref="ContentItem"/>.</param>
    /// <typeparam name="TFieldSet">The type of the field set.</typeparam>
    public static void Remove<TFieldSet>(this ContentItem contentItem) where TFieldSet : ContentFieldSet, new()
        => contentItem.Remove(ContentFieldSetName.Of<TFieldSet>());

    /// <summary>
    /// Adds a field set by its type.
    /// </summary>
    /// <param name="contentItem">The <see cref="ContentItem"/>.</param>
    /// <param name="part">The field set to weld.</param>
    /// <typeparam name="TFieldSet">The type of the field set.</typeparam>
    /// <returns>The current <see cref="ContentItem"/> instance.</returns>
    public static ContentItem Weld<TFieldSet>(this ContentItem contentItem, TFieldSet part) where TFieldSet : ContentFieldSet
    {
        contentItem.Weld(ContentFieldSetName.Of<TFieldSet>(), part);

        return contentItem;
    }

    /// <summary>
    /// Updates the field set with the specified type.
    /// </summary>
    /// <param name="contentItem">The <see cref="ContentItem"/>.</param>
    /// <param name="part">The field set to update.</param>
    /// <typeparam name="TFieldSet">The type of the field set.</typeparam>
    /// <returns>The current <see cref="ContentItem"/> instance.</returns>
    public static ContentItem Apply<TFieldSet>(this ContentItem contentItem, TFieldSet part) where TFieldSet : ContentFieldSet
    {
        contentItem.Apply(ContentFieldSetName.Of<TFieldSet>(), part);

        return contentItem;
    }

    /// <summary>
    /// Modifies a new or existing field set by name.
    /// </summary>
    /// <param name="contentItem">The <see cref="ContentItem"/>.</param>
    /// <param name="action">An action to apply on the field set.</param>
    /// <typeparam name="TFieldSet">The type of the field set.</typeparam>
    /// <returns>The current <see cref="ContentFieldSet"/> instance.</returns>
    public static ContentItem Alter<TFieldSet>(this ContentItem contentItem, Action<TFieldSet> action) where TFieldSet : ContentFieldSet, new()
    {
        var part = contentItem.GetOrCreate<TFieldSet>();
        action(part);
        contentItem.Apply(part);

        return contentItem;
    }

    /// <summary>
    /// Modifies a new or existing field set by name.
    /// </summary>
    /// <param name="contentItem">The <see cref="ContentItem"/>.</param>
    /// <param name="action">An action to apply on the field set.</param>
    /// <typeparam name="TFieldSet">The type of the field set.</typeparam>
    /// <returns>The current <see cref="ContentItem"/> instance.</returns>
    public static async Task<ContentItem> AlterAsync<TFieldSet>(this ContentItem contentItem, Func<TFieldSet, Task> action) where TFieldSet : ContentFieldSet, new()
    {
        var part = contentItem.GetOrCreate<TFieldSet>();
        await action(part);
        contentItem.Apply(part);

        return contentItem;
    }

    /// <summary>
    /// Merges properties to the contents of a content item.
    /// </summary>
    /// <param name="contentItem">The <see cref="ContentItem"/>.</param>
    /// <param name="properties">The object to merge.</param>
    /// <param name="settings">The <see cref="JsonMergeSettings"/> to use.</param>
    /// <returns>The current <see cref="ContentItem"/> instance.</returns>
    public static ContentItem Merge(this ContentItem contentItem, object properties, JsonMergeSettings settings = null)
    {
        var props = JObject.FromObject(properties);

        var originalDocumentId = contentItem.Id;
        contentItem.Data.Merge(props, settings);
        contentItem.Elements.Clear();

        // Return to original value or it will be interpreted as a different object by YesSql.
        contentItem.Id = originalDocumentId;

        // After merging content here we need to remove all the well known properties from the Data jObject
        // or these properties will take precedence over the properties on the C# object when and if they are mutated.
        if (props.ContainsKey(nameof(contentItem.DisplayText)))
        {
            contentItem.DisplayText = props[nameof(contentItem.DisplayText)]?.ToString();
            contentItem.Data.Remove(nameof(contentItem.DisplayText));
        }

        if (props.ContainsKey(nameof(contentItem.Owner)))
        {
            contentItem.Owner = props[nameof(contentItem.Owner)]?.ToString();
            contentItem.Data.Remove(nameof(contentItem.Owner));
        }

        if (props.ContainsKey(nameof(contentItem.Author)))
        {
            contentItem.Author = props[nameof(contentItem.Author)]?.ToString();
            contentItem.Data.Remove(nameof(contentItem.Author));
        }

        // Do not set these properties on the content item as they are the responsibility of the content manager.
        if (props.ContainsKey(nameof(contentItem.Published)))
        {
            contentItem.Data.Remove(nameof(contentItem.Published));
        }

        if (props.ContainsKey(nameof(contentItem.Latest)))
        {
            contentItem.Data.Remove(nameof(contentItem.Latest));
        }

        if (props.ContainsKey(nameof(contentItem.CreatedUtc)))
        {
            contentItem.Data.Remove(nameof(contentItem.CreatedUtc));
        }

        if (props.ContainsKey(nameof(contentItem.ModifiedUtc)))
        {
            contentItem.Data.Remove(nameof(contentItem.ModifiedUtc));
        }

        if (props.ContainsKey(nameof(contentItem.PublishedUtc)))
        {
            contentItem.Data.Remove(nameof(contentItem.PublishedUtc));
        }

        if (props.ContainsKey(nameof(contentItem.ContentItemId)))
        {
            contentItem.Data.Remove(nameof(contentItem.ContentItemId));
        }

        if (props.ContainsKey(nameof(contentItem.ContentItemVersionId)))
        {
            contentItem.Data.Remove(nameof(contentItem.ContentItemVersionId));
        }

        if (props.ContainsKey(nameof(contentItem.ContentType)))
        {
            contentItem.Data.Remove(nameof(contentItem.ContentType));
        }

        return contentItem;
    }

    /// <summary>
    /// Creates an in-memory clone of a content item.
    /// </summary>
    /// <param name="contentItem"></param>
    /// <returns></returns>
    public static ContentItem Clone(this ContentItem contentItem)
    {
        return new ContentItem
        {
            Id = contentItem.Id,
            ContentItemId = contentItem.ContentItemId,
            ContentItemVersionId = contentItem.ContentItemVersionId,
            ContentType = contentItem.ContentType,
            DisplayText = contentItem.DisplayText,
            Owner = contentItem.Owner,
            Author = contentItem.Author,
            Published = contentItem.Published,
            Latest = contentItem.Latest,
            CreatedUtc = contentItem.CreatedUtc,
            ModifiedUtc = contentItem.ModifiedUtc,
            PublishedUtc = contentItem.PublishedUtc,
            Data = contentItem.Data.Clone(),
        };
    }
}
