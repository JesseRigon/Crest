using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace Grove.Components.Primitives
{
    /// <summary>
    /// Base class that GroveHtmlEditor color picker tools inherit from.
    /// </summary>
    public abstract class GroveHtmlEditorColorBase : GroveHtmlEditorButtonBase
    {
        /// <summary>
        /// Sets <see cref="GroveColorPicker.ShowHSV" /> of the built-in GroveColorPicker.
        /// </summary>
        [Parameter]
        public bool ShowHSV { get; set; } = true;

        /// <summary>
        /// Sets <see cref="GroveColorPicker.ShowRGBA" /> of the built-in GroveColorPicker.
        /// </summary>
        [Parameter]
        public bool ShowRGBA { get; set; } = true;

        /// <summary>
        /// Gets or sets the child content.
        /// </summary>
        /// <value>The child content.</value>
        [Parameter]
        public RenderFragment? ChildContent { get; set; }

        /// <summary>
        /// Sets <see cref="GroveColorPicker.ShowColors" /> of the built-in GroveColorPicker.
        /// </summary>
        [Parameter]
        public bool ShowColors { get; set; } = true;

        /// <summary>
        /// Sets <see cref="GroveColorPicker.ShowButton" /> of the built-in GroveColorPicker.
        /// </summary>
        [Parameter]
        public bool ShowButton { get; set; } = true;

        private string? hexText;

        /// <summary>
        /// Sets <see cref="GroveColorPicker.HexText" /> of the built-in GroveColorPicker.
        /// </summary>
        [Parameter]
        public string HexText { get => hexText ?? Localize(nameof(GroveStrings.HtmlEditorColor_HexText)); set => hexText = value; }

        private string? redText;

        /// <summary>
        /// Sets <see cref="GroveColorPicker.RedText" /> of the built-in GroveColorPicker.
        /// </summary>
        [Parameter]
        public string RedText { get => redText ?? Localize(nameof(GroveStrings.HtmlEditorColor_RedText)); set => redText = value; }

        private string? greenText;

        /// <summary>
        /// Sets <see cref="GroveColorPicker.GreenText" /> of the built-in GroveColorPicker.
        /// </summary>
        [Parameter]
        public string GreenText { get => greenText ?? Localize(nameof(GroveStrings.HtmlEditorColor_GreenText)); set => greenText = value; }

        private string? blueText;

        /// <summary>
        /// Sets <see cref="GroveColorPicker.BlueText" /> of the built-in GroveColorPicker.
        /// </summary>
        [Parameter]
        public string BlueText { get => blueText ?? Localize(nameof(GroveStrings.HtmlEditorColor_BlueText)); set => blueText = value; }

        private string? alphaText;

        /// <summary>
        /// Sets <see cref="GroveColorPicker.AlphaText" /> of the built-in GroveColorPicker.
        /// </summary>
        [Parameter]
        public string AlphaText { get => alphaText ?? Localize(nameof(GroveStrings.HtmlEditorColor_AlphaText)); set => alphaText = value; }

        private string? buttonText;

        /// <summary>
        /// Sets <see cref="GroveColorPicker.ButtonText" /> of the built-in GroveColorPicker.
        /// </summary>
        [Parameter]
        public string ButtonText { get => buttonText ?? Localize(nameof(GroveStrings.HtmlEditorColor_ButtonText)); set => buttonText = value; }


        /// <summary>
        /// Handles the change event of built-in GroveColorPicker.
        /// </summary>
        /// <param name="value">The new color.</param>
        protected virtual async Task OnChange(string value)
        {
            if (Editor != null && CommandName != null)
            {
                await Editor.ExecuteCommandAsync(CommandName, value);
            }
        }

        /// <summary>
        /// The default value of the color picker.
        /// </summary>
        public abstract string Value { get; set; }

        /// <summary>
        /// The internal state of the component.
        /// </summary>
        protected string? value;

        /// <inheritdoc />
        protected override void OnInitialized()
        {
            value = Value;

            base.OnInitialized();
        }

        /// <inheritdoc />
        public override async Task SetParametersAsync(ParameterView parameters)
        {
            var valueChanged = parameters.DidParameterChange(nameof(Value), Value);

            await base.SetParametersAsync(parameters);

            if (valueChanged)
            {
                value = Value;
            }
        }
    }
}
