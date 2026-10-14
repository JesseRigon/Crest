using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Crest.Components.Primitives;
using Crest.DisplayManagement.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Components.Editors;

/// <summary>One user a search answered.</summary>
public sealed record UserPickerResult(string Id, string DisplayText, bool IsEnabled);

/// <summary>
/// The editor of a <c>UserPickerField</c> (docs/blazor-display.md rework step 7): users found by
/// a search (<c>api/crest/content-fields/users</c>, the field's result provider and its role
/// filter, under the type's edit permission) and kept as the field's <c>UserIds</c> with their
/// <c>UserNames</c>; one user, or several when the field allows it.
/// </summary>
[BlazorComponent("UserPickerFieldEditor", DisplayName = "Users", Category = "Editors", Icon = "group", Edits = ["UserPickerField"])]
public sealed class UserPickerFieldEditor : FieldEditorBase
{
    private string _query = string.Empty;
    private IReadOnlyList<UserPickerResult> _results = [];
    private string? _error;

    [Inject]
    private IServiceProvider Services { get; set; } = null!;

    private bool Multiple => Field?.Settings?["UserPickerFieldSettings"]?["Multiple"] is JsonValue multiple && multiple.TryGetValue<bool>(out var value) && value;

    private List<string> Strings(string property) =>
        Value?[property] is JsonArray values ? [.. values.Select(value => value?.ToString() ?? string.Empty)] : [];

    protected override void RenderInput(RenderTreeBuilder builder)
    {
        var ids = Strings("UserIds");
        var names = Strings("UserNames");
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "crest-user-picker");
        builder.OpenElement(2, "ul");
        builder.AddAttribute(3, "class", "crest-user-picker__chosen");
        for (var index = 0; index < ids.Count; index++)
        {
            var position = index;
            builder.OpenElement(4, "li");
            builder.SetKey(ids[index]);
            builder.AddContent(5, index < names.Count && names[index].Length > 0 ? names[index] : ids[index]);
            builder.OpenComponent<CrestButton>(6);
            builder.AddComponentParameter(7, nameof(CrestButton.Icon), "close");
            builder.AddComponentParameter(8, nameof(CrestButton.Variant), Variant.Text);
            builder.AddComponentParameter(9, nameof(CrestButton.Size), ButtonSize.ExtraSmall);
            builder.AddComponentParameter(10, nameof(CrestButton.Disabled), Disabled);
            builder.AddComponentParameter(11, nameof(CrestButton.Click), EventCallback.Factory.Create<MouseEventArgs>(this, () => Remove(position)));
            builder.CloseComponent();
            builder.CloseElement();
        }

        builder.CloseElement();

        builder.OpenComponent<CrestTextBox>(12);
        builder.AddComponentParameter(13, nameof(CrestTextBox.Value), _query);
        builder.AddComponentParameter(14, nameof(CrestTextBox.Placeholder), "Search users");
        builder.AddComponentParameter(15, nameof(CrestTextBox.Disabled), Disabled);
        builder.AddComponentParameter(16, nameof(CrestTextBox.Change), EventCallback.Factory.Create<string>(this, SearchAsync));
        builder.CloseComponent();

        builder.OpenElement(17, "ul");
        builder.AddAttribute(18, "class", "crest-user-picker__results");
        foreach (var result in _results.Where(result => !ids.Contains(result.Id)))
        {
            builder.OpenElement(19, "li");
            builder.SetKey(result.Id);
            builder.OpenComponent<CrestButton>(20);
            builder.AddComponentParameter(21, nameof(CrestButton.Text), result.DisplayText);
            builder.AddComponentParameter(22, nameof(CrestButton.Variant), Variant.Text);
            builder.AddComponentParameter(23, nameof(CrestButton.Size), ButtonSize.Small);
            builder.AddComponentParameter(24, nameof(CrestButton.Disabled), Disabled || !result.IsEnabled);
            builder.AddComponentParameter(25, nameof(CrestButton.Click), EventCallback.Factory.Create<MouseEventArgs>(this, () => Choose(result)));
            builder.CloseComponent();
            builder.CloseElement();
        }

        builder.CloseElement();
        if (_error is not null)
        {
            builder.OpenElement(26, "div");
            builder.AddAttribute(27, "class", "crest-editor__error rz-color-danger");
            builder.AddContent(28, _error);
            builder.CloseElement();
        }

        builder.CloseElement();
    }

    private async Task SearchAsync(string query)
    {
        _query = query;
        _error = null;
        if (Services.GetService<HttpClient>() is not { } http || Context?.Metadata?.ContentType is not { } contentType || FieldName is null)
        {
            return;
        }

        try
        {
            var url = $"api/crest/content-fields/users?part={Uri.EscapeDataString(PartName)}&field={Uri.EscapeDataString(FieldName)}&contentType={Uri.EscapeDataString(contentType)}&query={Uri.EscapeDataString(query)}";
            _results = await http.GetFromJsonAsync<List<UserPickerResult>>(url) ?? [];
        }
        catch (HttpRequestException exception)
        {
            _results = [];
            _error = exception.Message;
        }
    }

    private void Choose(UserPickerResult user)
    {
        var (ids, names) = Multiple ? (Strings("UserIds"), Strings("UserNames")) : ([], []);
        while (names.Count < ids.Count)
        {
            names.Add(string.Empty);
        }

        ids.Add(user.Id);
        names.Add(user.DisplayText);
        Write(ids, names);
    }

    private void Remove(int index)
    {
        var ids = Strings("UserIds");
        var names = Strings("UserNames");
        ids.RemoveAt(index);
        if (index < names.Count)
        {
            names.RemoveAt(index);
        }

        Write(ids, names);
    }

    private void Write(List<string> ids, List<string> names)
    {
        Set("UserIds", new JsonArray([.. ids.Select(id => (JsonNode)JsonValue.Create(id))]));
        Set("UserNames", new JsonArray([.. names.Select(name => (JsonNode)JsonValue.Create(name))]));
    }
}
