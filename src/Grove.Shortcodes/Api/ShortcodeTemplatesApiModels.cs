namespace Grove.Shortcodes.Api;

// The shortcode templates API's wire types, shared with the admin page (blazor-wasm links this file).

public static class ShortcodeTemplatesApiPaths
{
    public const string Templates = "api/grove/shortcodes/templates";

    public static string Template(string name) => $"{Templates}/{Uri.EscapeDataString(name)}";
}

/// <summary>A shortcode template: <c>[name]</c> renders <see cref="Content"/> (Liquid).</summary>
public sealed class ShortcodeTemplateModel
{
    public string Name { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string? Hint { get; set; }

    public string? Usage { get; set; }

    public string? DefaultValue { get; set; }

    public string[] Categories { get; set; } = [];
}

/// <summary>The templates and the categories in use by any shortcode, for the editor's suggestions.</summary>
public sealed record ShortcodeTemplatesModel(ShortcodeTemplateModel[] Templates, string[] Categories);
