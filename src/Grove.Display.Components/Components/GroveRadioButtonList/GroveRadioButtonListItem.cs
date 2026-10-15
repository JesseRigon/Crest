using Microsoft.AspNetCore.Components;
using Grove.Display.Components.Primitives.Rendering;
using System;
using System.Collections.Generic;

namespace Grove.Display.Components.Primitives
{
    /// <summary>
    /// GroveRadioButtonListItem component.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    public class GroveRadioButtonListItem<TValue> : GroveComponent, IGroveRadioButtonListItem
    {
        /// <summary>
        /// Specifies additional custom attributes that will be rendered by the input.
        /// </summary>
        /// <value>The attributes.</value>
        [Parameter]
        public IReadOnlyDictionary<string, object>? InputAttributes { get; set; }

        private string? text;

        /// <summary>
        /// Gets or sets the text.
        /// </summary>
        /// <value>The text.</value>
        [Parameter]
        public string? Text
        {
            get
            {
                return text;
            }
            set
            {
                if (value != text)
                {
                    text = value;

                    if (List != null)
                    {
                        List.Refresh();
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the template.
        /// </summary>
        /// <value>The template.</value>
        [Parameter]
        public RenderFragment<GroveRadioButtonListItem<TValue>>? Template { get; set; }

        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        /// <value>The value.</value>
        [Parameter]
        public TValue? Value { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="GroveRadioButtonListItem{TValue}"/> is disabled.
        /// </summary>
        /// <value><c>true</c> if disabled; otherwise, <c>false</c>.</value>
        [Parameter]
        public virtual bool Disabled { get; set; }

        private IGroveRadioButtonList? list;

        /// <summary>
        /// Gets or sets the list.
        /// </summary>
        /// <value>The list.</value>
        [CascadingParameter]
        public IGroveRadioButtonList? List
        {
            get
            {
                return list;
            }
            set
            {
                if (list != value)
                {
                    list = value;
                    list?.AddItem(this);
                }
            }
        }

        object? IGroveRadioButtonListItem.Value => Value;

        RenderFragment? IGroveRadioButtonListItem.Template => Template?.Invoke(this);

        ElementReference IGroveRadioButtonListItem.Element
        {
            get => Element;
            set => Element = value;
        }

        string? IGroveRadioButtonListItem.GetItemId() => GetItemId();

        string IGroveRadioButtonListItem.GetItemCssClass() => GetItemCssClass();

        /// <summary>
        /// Disposes this instance.
        /// </summary>
        public override void Dispose()
        {
            base.Dispose();
            List?.RemoveItem(this);
            GC.SuppressFinalize(this);
        }

        internal void SetText(string value)
        {
            Text = value;
        }

        internal void SetValue(TValue value)
        {
            Value = value;
        }

        internal void SetDisabled(bool value)
        {
            Disabled = value;
        }

        internal void SetVisible(bool value)
        {
            Visible = value;
        }

        internal string? GetItemId()
        {
            return GetId();
        }

        internal string GetItemCssClass()
        {
            return ClassList.Create(GetCssClass())
                .AddDisabled(Disabled)
                .ToString();
        }

        /// <inheritdoc />
        protected override string GetComponentCssClass()
        {
            return "rz-radio-btn";
        }
    }
}
