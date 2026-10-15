using Microsoft.AspNetCore.Components;
using System;

namespace Grove.Components.Primitives
{
    /// <summary>
    /// Configures a level of nodes in a <see cref="GroveTree" /> during data-binding.
    /// </summary>
    /// <example>
    /// <code>
    /// &lt;GroveTree Data=@rootEmployees&gt;
    ///     &lt;GroveTreeLevel TextProperty="LastName" ChildrenProperty="Employees1"  HasChildren=@(e =&gt; (e as Employee).Employees1.Any()) /&gt;
    /// &lt;/GroveTree&gt;
    /// @code {
    ///  IEnumerable&lt;Employee&gt; rootEmployees; 
    ///  protected override void OnInitialized()
    ///  {
    ///     rootEmployees = NorthwindDbContext.Employees.Where(e => e.ReportsTo == null);
    ///  }
    /// }
    /// </code>
    /// </example>
    public class GroveTreeLevel : ComponentBase
    {
        /// <summary>
        /// Specifies the name of the property which provides values for the <see cref="GroveTreeItem.Text" /> property of the child items.
        /// </summary>
        [Parameter]
        public string? TextProperty { get; set; }

        /// <summary>
        /// Specifies the name of the property which provides values for the <see cref="GroveTreeItem.Checkable" /> property of the child items.
        /// </summary>
        [Parameter]
        public string? CheckableProperty { get; set; }

        /// <summary>
        /// Specifies the name of the property which returns child data. The value returned by that property should be IEnumerable
        /// </summary>
        [Parameter]
        public string? ChildrenProperty { get; set; }

        /// <summary>
        /// Determines if a child item has children or not. Set to <c>value =&gt; true</c> by default.
        /// </summary>
        /// <example>
        /// <code>
        ///     &lt;GroveTreeLevel HasChildren=@(e =&gt; (e as Employee).Employees1.Any()) /&gt;
        /// </code>
        /// </example>
        [Parameter]
        public Func<object, bool>? HasChildren { get; set; } = value => true;

        /// <summary>
        /// Determines if a child item is expanded or not. Set to <c>value =&gt; false</c> by default.
        /// </summary>
        /// <example>
        /// <code>
        ///     &lt;GroveTreeLevel Expanded=@(e =&gt; (e as Employee).Employees1.Any()) /&gt;
        /// </code>
        /// </example>
        [Parameter]
        public Func<object, bool>? Expanded { get; set; } = value => false;

        /// <summary>
        /// Determines if a child item is selected or not. Set to <c>value =&gt; false</c> by default.
        /// </summary>
        /// <example>
        /// <code>
        ///     &lt;GroveTreeLevel Selected=@(e =&gt; (e as Employee).LastName == "Fuller") /&gt;
        /// </code>
        /// </example>
        [Parameter]
        public Func<object, bool>? Selected { get; set; } = value => false;

        /// <summary>
        /// Determines the text of a child item.
        /// </summary>
        /// <example>
        /// <code>
        ///     &lt;GroveTreeLevel Text=@(e =&gt; (e as Employee).LastName) /&gt;
        /// </code>
        /// </example>
        [Parameter]
        public Func<object, string>? Text { get; set; }

        /// <summary>
        /// Determines the if the checkbox of the child item can be checked.
        /// </summary>
        /// <example>
        /// <code>
        ///     &lt;GroveTreeLevel Checkable=@(e =&gt; (e as Employee).LastName != null) /&gt;
        /// </code>
        /// </example>
        [Parameter]
        public Func<object, bool>? Checkable { get; set; }

        /// <summary>
        /// Gets or sets the template.
        /// </summary>
        [Parameter]
        public RenderFragment<GroveTreeItem>? Template { get; set; }

        private GroveTree? _tree;

        /// <summary>
        /// The GroveTree which this item is part of.
        /// </summary>
        [CascadingParameter]
        public GroveTree? Tree
        {
            get => _tree;
            set
            {
                if (value != null)
                {
                    value.AddLevel(this);
                }

                _tree = value;
            }
        }
    }
}