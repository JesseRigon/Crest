using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grove.Components.Models;
using Grove.Components.Primitives;
using Grove.DisplayManagement.Components;
using Microsoft.AspNetCore.Components;

namespace Grove.Components.Editors;

/// <summary>
/// A settings group's form (docs/MVC/parity.md › SettingsForm): generated from the group's
/// settings schema (fields, sections, write-only secrets, the "saving reloads the tenant" notice),
/// loaded from and saved through <c>api/grove/settings/groups/{groupId}</c>. The server checks
/// the model's rules on save and answers the errors by field, shown under each field.
/// </summary>
[BlazorComponent("SettingsForm", DisplayName = "Settings form", Category = "Editors", Icon = "tune")]
public sealed partial class SettingsForm : ComponentBase
{
    private const string Api = "api/grove/settings/groups/";

    private static readonly JsonSerializerOptions s_web = new(JsonSerializerDefaults.Web);

    private SettingsFormModel? _form;
    private JsonObject _values = [];
    private Dictionary<string, bool> _secrets = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, List<string>> _errors = new(StringComparer.OrdinalIgnoreCase);
    private string? _error;
    private string? _loaded;
    private bool _notFound;
    private bool _saving;

    /// <summary>The settings group id.</summary>
    [Parameter, EditorRequired]
    [ComponentParameter(Required = true)]
    public string GroupId { get; set; } = string.Empty;

    /// <summary>What to show when the server has no such group (a group still served elsewhere).</summary>
    [Parameter]
    [ComponentParameter(Hidden = true)]
    public RenderFragment? NotFoundContent { get; set; }

    /// <summary>Raised after a save, with the stored values.</summary>
    [Parameter]
    [ComponentOutput(DisplayName = "Saved")]
    public EventCallback<JsonObject> Saved { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        if (string.Equals(_loaded, GroupId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _loaded = GroupId;
        _form = null;
        _error = null;
        _notFound = false;
        _errors.Clear();
        if (string.IsNullOrWhiteSpace(GroupId))
        {
            return;
        }

        try
        {
            using var response = await Http.GetAsync(Api + Uri.EscapeDataString(GroupId));
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _notFound = true;
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                _error = response.StatusCode == HttpStatusCode.Forbidden ? T["You are not allowed to manage these settings."] : await response.Content.ReadAsStringAsync();
                return;
            }

            _form = await response.Content.ReadFromJsonAsync<SettingsFormModel>(s_web);
            _values = _form?.Values ?? [];
            _secrets = new Dictionary<string, bool>(_form?.Secrets ?? [], StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or NotSupportedException)
        {
            _error = exception.Message;
        }
    }

    private async Task SaveAsync()
    {
        if (_form is null || _saving)
        {
            return;
        }

        _saving = true;
        _error = null;
        _errors = Required(_form.Schema.Fields, _values, string.Empty);
        try
        {
            if (_errors.Count > 0)
            {
                return;
            }

            using var response = await Http.PutAsJsonAsync(Api + Uri.EscapeDataString(GroupId), new { Values = _values });
            if (response.IsSuccessStatusCode)
            {
                var saved = await response.Content.ReadFromJsonAsync<SettingsSavedModel>(s_web);
                if (saved is not null)
                {
                    _values = saved.Values;
                    _secrets = new Dictionary<string, bool>(saved.Secrets ?? [], StringComparer.OrdinalIgnoreCase);
                }

                Notifications.Notify(NotificationSeverity.Success, T["Settings saved"], saved?.Reloaded == true ? T["The settings were saved; the tenant reloads."] : T["The settings were saved."]);
                await Saved.InvokeAsync(_values);
                return;
            }

            _errors = await ReadErrorsAsync(response);
            if (_errors.Count == 0)
            {
                _error = await response.Content.ReadAsStringAsync();
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            _error = exception.Message;
        }
        finally
        {
            _saving = false;
        }
    }

    /// <summary>The problem details' <c>errors</c> map (keys are wire names, matched without case).</summary>
    internal static async Task<Dictionary<string, List<string>>> ReadErrorsAsync(HttpResponseMessage response)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (await response.Content.ReadFromJsonAsync<JsonObject>() is { } problem && problem["errors"] is JsonObject map)
            {
                foreach (var (key, messages) in map)
                {
                    errors[key] = messages is JsonArray array ? [.. array.Select(message => message?.ToString() ?? string.Empty)] : [messages?.ToString() ?? string.Empty];
                }
            }
        }
        catch (JsonException)
        {
        }

        return errors;
    }

    /// <summary>The required fields left empty, checked before anything is sent.</summary>
    internal Dictionary<string, List<string>> Required(IReadOnlyList<SettingsFieldModel> fields, JsonObject values, string prefix)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields.Where(field => IsVisible(field, values)))
        {
            var key = Join(prefix, field.Name);
            if (field.Required && !field.ReadOnly && !(field.Secret && IsSecretSet(key)) && IsEmpty(Get(values, field.Name)))
            {
                errors[key] = [string.Format(CultureInfo.CurrentCulture, T["{0} is required."], T[field.Label])];
            }
        }

        return errors;
    }

    private static bool IsEmpty(JsonNode? node) => node switch
    {
        null => true,
        JsonValue value when value.TryGetValue<string>(out var text) => string.IsNullOrWhiteSpace(text),
        JsonArray array => array.Count == 0,
        _ => false,
    };

    internal static bool IsVisible(SettingsFieldModel field, JsonObject values)
    {
        if (field.VisibleWhen is not { } condition)
        {
            return true;
        }

        var other = Get(values, condition.Field);
        if (condition.Values is not { Count: > 0 } allowed)
        {
            return other switch
            {
                null => false,
                JsonValue value when value.TryGetValue<bool>(out var flag) => flag,
                JsonValue value when value.TryGetValue<string>(out var text) => !string.IsNullOrEmpty(text),
                JsonArray array => array.Count > 0,
                _ => true,
            };
        }

        var current = other is JsonValue scalar && scalar.TryGetValue<string>(out var currentText) ? currentText : other?.ToJsonString();
        return allowed.Contains(current ?? string.Empty, StringComparer.OrdinalIgnoreCase);
    }

    private List<string> Errors(string key) => _errors.TryGetValue(key, out var messages) ? messages : [];

    private bool IsSecretSet(string key) => _secrets.TryGetValue(key, out var set) && set;

    internal static string Join(string prefix, string name) => prefix.Length == 0 ? name : prefix + "." + name;

    private string Id(string key) => "setting-" + GroupId + "-" + key.Replace('.', '-').Replace('[', '-').Replace("]", string.Empty, StringComparison.Ordinal);

    internal static JsonNode? Get(JsonObject values, string name)
    {
        foreach (var (key, value) in values)
        {
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }

    private void Set(JsonObject values, SettingsFieldModel field, JsonNode? value)
    {
        var existing = values.FirstOrDefault(pair => string.Equals(pair.Key, field.Name, StringComparison.OrdinalIgnoreCase)).Key;
        values[existing ?? field.Name] = value;
        StateHasChanged();
    }

    private static JsonNode? Json(string? text) => text is null ? null : JsonValue.Create(text);

    private static string? Text(JsonObject values, SettingsFieldModel field) => Get(values, field.Name) switch
    {
        null => null,
        JsonValue value when value.TryGetValue<string>(out var text) => text,
        var node => node.ToJsonString(),
    };

    private static string JsonText(JsonObject values, SettingsFieldModel field) => Get(values, field.Name) switch
    {
        null => string.Empty,
        JsonValue value when value.TryGetValue<string>(out var text) => text,
        var node => node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
    };

    private static bool Bool(JsonObject values, SettingsFieldModel field) =>
        Get(values, field.Name) is JsonValue value && value.TryGetValue<bool>(out var flag) && flag;

    private static decimal? Number(JsonObject values, SettingsFieldModel field) =>
        Get(values, field.Name) is JsonValue value
            ? value.TryGetValue<decimal>(out var number) ? number
            : value.TryGetValue<string>(out var text) && decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed
            : null
            : null;

    private static IEnumerable<string> Strings(JsonObject values, SettingsFieldModel field) =>
        Get(values, field.Name) is JsonArray array
            ? [.. array.Select(item => item?.ToString()).Where(item => !string.IsNullOrEmpty(item)).Select(item => item!)]
            : [];

    private static string Lines(JsonObject values, SettingsFieldModel field) => string.Join("\n", Strings(values, field));

    private static JsonArray LinesToArray(string? text) =>
        new([.. (text ?? string.Empty).Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(line => (JsonNode?)JsonValue.Create(line))]);

    private JsonObject Object(JsonObject values, SettingsFieldModel field)
    {
        if (Get(values, field.Name) is JsonObject json)
        {
            return json;
        }

        var created = new JsonObject();
        values[field.Name] = created;
        return created;
    }

    private JsonArray ItemsOf(JsonObject values, SettingsFieldModel field)
    {
        if (Get(values, field.Name) is JsonArray array)
        {
            return array;
        }

        var created = new JsonArray();
        values[field.Name] = created;
        return created;
    }

    private void AddItem(JsonObject values, SettingsFieldModel field)
    {
        var item = new JsonObject();
        foreach (var child in field.Fields ?? [])
        {
            item[child.Name] = child.Kind switch
            {
                "boolean" => JsonValue.Create(false),
                "objects" or "list" or "multiselect" => new JsonArray(),
                "select" when child.Required && child.Options is { Count: > 0 } options => JsonValue.Create(options[0].Value),
                _ => null,
            };
        }

        ItemsOf(values, field).Add(item);
        StateHasChanged();
    }

    private void RemoveItem(JsonObject values, SettingsFieldModel field, int index)
    {
        var array = ItemsOf(values, field);
        if (index >= 0 && index < array.Count)
        {
            array.RemoveAt(index);
        }

        _errors.Clear();
        StateHasChanged();
    }
}
