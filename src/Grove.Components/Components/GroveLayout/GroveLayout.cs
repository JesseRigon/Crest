using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Grove.Components.Primitives.Rendering;
using System;

namespace Grove.Components.Primitives
{
    /// <summary>
    /// A layout container component that defines the overall structure of a Blazor application with header, sidebar, body, and footer sections.
    /// GroveLayout is typically used in MainLayout.razor to create a consistent page structure with optional collapsible sidebar and theme integration.
    /// Works with companion components: GroveHeader, GroveSidebar, GroveBody, and GroveFooter. Automatically integrates with ThemeService to apply theme-specific CSS classes.
    /// All sections are optional and can be used in any combination to create the desired page structure. The sidebar can be configured as collapsible, and the layout adjusts automatically when the sidebar expands or collapses.
    /// </summary>
    /// <example>
    /// Basic layout with all sections:
    /// <code>
    /// &lt;GroveLayout&gt;
    ///     &lt;GroveHeader&gt;
    ///         &lt;h1&gt;My Application&lt;/h1&gt;
    ///     &lt;/GroveHeader&gt;
    ///     &lt;GroveSidebar&gt;
    ///         &lt;GrovePanelMenu&gt;
    ///             @* Navigation menu items *@
    ///         &lt;/GrovePanelMenu&gt;
    ///     &lt;/GroveSidebar&gt;
    ///     &lt;GroveBody&gt;
    ///         @Body
    ///     &lt;/GroveBody&gt;
    ///     &lt;GroveFooter&gt;
    ///         © 2026 My Company
    ///     &lt;/GroveFooter&gt;
    /// &lt;/GroveLayout&gt;
    /// </code>
    /// </example>
    // Named App* so the app-frame hosts do not collide with the themes' Layout component or the zone keys.
    [Grove.DisplayManagement.Components.BlazorComponent("AppLayout", DisplayName = "App layout", Category = "Layout", Icon = "layout_dashboard", IsHost = true)]
    public partial class GroveLayout : GroveComponentWithChildren, IDisposable
    {
        [Inject]
        private IServiceProvider? ServiceProvider { get; set; }

        private ThemeService? themeService;

        /// <inheritdoc />
        protected override void OnInitialized()
        {
            themeService = ServiceProvider?.GetService<ThemeService>();

            if (themeService != null)
            {
                themeService.ThemeChanged += OnThemeChanged;
            }

            base.OnInitialized();
        }

        private void OnThemeChanged()
        {
            StateHasChanged();
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            if (themeService != null)
            {
                themeService.ThemeChanged -= OnThemeChanged;
            }

            base.Dispose();
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc />
        protected override string GetComponentCssClass() => ClassList.Create("rz-layout")
            .Add($"rz-{themeService?.Theme}", themeService != null)
            .ToString();
    }
}