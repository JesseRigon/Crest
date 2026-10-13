using Crest.DisplayManagement.Components;
using Microsoft.AspNetCore.Components;

#nullable enable

namespace Crest.Tests.DisplayManagement.Components;

// Components this test assembly declares. Crest.Tests runs the source generator, so the
// assembly carries a generated catalog for them as well; the tests compare it with the
// reflection fallback.

public enum CardTone
{
    Neutral = 0,
    Accent = 2,
    Warning = 1,
}

public abstract class CardBase : ComponentBase
{
    [Parameter]
    [ComponentParameter(Group = "Appearance", Advanced = true)]
    public string? Style { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? Attributes { get; set; }
}

[BlazorComponent("Test__Card", DisplayName = "Card", Category = "Layout", Icon = "card", IsHost = true, Instances = ComponentInstances.Single, Permission = "ViewCards")]
public sealed class TestCard : CardBase
{
    [Parameter]
    [ComponentParameter(DisplayName = "Title", Group = "Content", Required = true)]
    public string? Title { get; set; }

    [Parameter]
    public int Count { get; set; }

    [Parameter]
    public decimal? Amount { get; set; }

    [Parameter]
    public bool Elevated { get; set; }

    [Parameter]
    public CardTone Tone { get; set; }

    [Parameter]
    [ComponentParameter(Choices = ["Small", "Large"])]
    public string Size { get; set; } = "Small";

    [Parameter]
    [EditorRequired]
    public DateTime Since { get; set; }

    [Parameter]
    public IReadOnlyList<string>? Tags { get; set; }

    [Parameter]
    [ComponentParameter(Editor = "picker", Accepts = ["ContentItem"])]
    public object? Source { get; set; }

    [Parameter]
    public EventCallback<int> Clicked { get; set; }

    [Parameter]
    [ComponentZone(Allowed = ["Text", "Image"], Min = 1, Max = 3)]
    public RenderFragment? Body { get; set; }

    [Parameter]
    public RenderFragment<string>? Footer { get; set; }

    [CascadingParameter]
    public string? Cascaded { get; set; }

    public string? NotAParameter { get; set; }
}

[BlazorComponent("Test__Plain")]
public sealed class TestPlain : ComponentBase
{
    [Parameter]
    public string? Text { get; set; }
}

[BlazorComponent("Test__Plain__Summary")]
public sealed class TestPlainSummary : ComponentBase
{
}

public sealed class TestUnmarked : ComponentBase
{
    [Parameter]
    public string? Text { get; set; }
}

[BlazorComponent("Test__List")]
public sealed class TestList : ComponentBase
{
    [Parameter]
    [ComponentOutput(DisplayName = "Selection")]
    public EventCallback<string> Selected { get; set; }

    [Parameter]
    public EventCallback<int> Pressed { get; set; }
}

[BlazorComponent("Test__Menu", IsOverlay = true)]
public sealed class TestMenu : ComponentBase
{
    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}

[BlazorComponent("Test__Editor", Edits = ["TestField", "OtherField"])]
public sealed class TestEditor : ComponentBase
{
    [Parameter]
    public string? PartName { get; set; }
}
