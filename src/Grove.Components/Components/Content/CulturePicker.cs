using System.Net.Http.Json;
using System.Text.Json;
using Grove.DisplayManagement.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Components.Content;

/// <summary>
/// The site's culture switcher (docs/MVC/parity.md › New components): the site's cultures, the
/// current one marked, each a link to <c>RedirectToLocalizedContent</c>, which opens the current
/// page's translation in that culture (or the home page's, as the culture picker settings say).
/// The cultures come from Grove.ContentLocalization's <c>api/grove/content-localization/cultures</c>;
/// the picker renders nothing while the site has one culture or the feature is off.
/// </summary>
[BlazorComponent("CulturePicker", DisplayName = "Culture picker", Category = "Navigation", Icon = "translate")]
public sealed class CulturePicker : ComponentBase
{
    private static readonly JsonSerializerOptions s_web = new(JsonSerializerDefaults.Web);

    private IReadOnlyList<SiteCulture> _cultures = [];

    [Parameter]
    [ComponentParameter(Advanced = true)]
    public string? Class { get; set; }

    [Inject]
    private IServiceProvider Services { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    protected override async Task OnInitializedAsync()
    {
        if (Services.GetService<HttpClient>() is not { } http)
        {
            return;
        }

        try
        {
            using var response = await http.GetAsync("api/grove/content-localization/cultures");
            if (response.IsSuccessStatusCode)
            {
                _cultures = await response.Content.ReadFromJsonAsync<SiteCulture[]>(s_web) ?? [];
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException)
        {
        }
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (_cultures.Count < 2)
        {
            return;
        }

        var path = "/" + Navigation.ToBaseRelativePath(Navigation.Uri).Split('?', '#')[0];
        builder.OpenElement(0, "nav");
        builder.AddAttribute(1, "class", string.IsNullOrEmpty(Class) ? "grove-culture-picker" : "grove-culture-picker " + Class);
        builder.AddAttribute(2, "aria-label", "Language");
        builder.OpenElement(3, "ul");
        foreach (var culture in _cultures)
        {
            builder.OpenElement(4, "li");
            builder.SetKey(culture.Name);
            if (culture.Current)
            {
                builder.OpenElement(5, "span");
                builder.AddAttribute(6, "aria-current", "true");
                builder.AddAttribute(7, "lang", culture.Name);
                builder.AddContent(8, culture.NativeName);
                builder.CloseElement();
            }
            else
            {
                builder.OpenElement(9, "a");
                builder.AddAttribute(10, "href", $"RedirectToLocalizedContent?targetCulture={Uri.EscapeDataString(culture.Name)}&contentItemUrl={Uri.EscapeDataString(path)}");
                builder.AddAttribute(11, "lang", culture.Name);
                builder.AddAttribute(12, "data-enhance-nav", "false");
                builder.AddContent(13, culture.NativeName);
                builder.CloseElement();
            }

            builder.CloseElement();
        }

        builder.CloseElement();
        builder.CloseElement();
    }

    /// <summary>A site culture as the culture picker API serves it.</summary>
    public sealed record SiteCulture(string Name, string NativeName, bool Current);
}
