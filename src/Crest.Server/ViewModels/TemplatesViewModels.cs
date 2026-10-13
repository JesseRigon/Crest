using Crest.DisplayManagement.Components;

namespace Crest.ViewModels;

/// <summary>A template as the Templates page lists it (docs/blazor-display.md § 5).</summary>
public sealed record CrestTemplate(
    string Id,
    string Name,
    string? Theme,
    string? DisplayType,
    string? Description,
    string Source,
    bool Published,
    bool HasDraft,
    DateTime? ModifiedUtc,
    bool IsComposite,
    string? DisplayName,
    string? Category,
    string? Icon,
    IReadOnlyList<Crest.Templates.Models.TemplateInput> Inputs,
    IReadOnlyList<Crest.Templates.Models.TemplateOutput> Outputs);

public sealed record CrestTemplateWrite(
    string Name,
    string? Theme,
    string? DisplayType,
    string? Description,
    string? Source,
    bool IsComposite = false,
    string? DisplayName = null,
    string? Category = null,
    string? Icon = null,
    List<Crest.Templates.Models.TemplateInput>? Inputs = null,
    List<Crest.Templates.Models.TemplateOutput>? Outputs = null)
{
    public Crest.Templates.Models.TemplatePart ToPart() => new()
    {
        Name = Name,
        Theme = Theme,
        DisplayType = DisplayType,
        Description = Description?.Trim(),
        Source = Source ?? string.Empty,
        IsComposite = IsComposite,
        DisplayName = DisplayName,
        Category = Category,
        Icon = Icon,
        Inputs = Inputs ?? [],
        Outputs = Outputs ?? [],
    };
}

/// <summary>Evaluates a draft for preview: bound to its name for one content item, or on its own.</summary>
public sealed record CrestTemplatePreview(string Name, string Source, string? ContentItemId, string? DisplayType);

public sealed record CrestTemplateValidation(bool Valid, IReadOnlyList<DisplayDiagnostic> Diagnostics);
