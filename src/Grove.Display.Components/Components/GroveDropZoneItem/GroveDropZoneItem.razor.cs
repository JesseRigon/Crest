using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using System.Threading.Tasks;

namespace Grove.Display.Components.Primitives
{
    /// <summary>
    /// GroveDropZoneItem component.
    /// </summary>
    public partial class GroveDropZoneItem<TItem> : GroveComponent
    {
        [CascadingParameter]
        TItem Item { get; set; } = default!;

        [CascadingParameter]
        GroveDropZone<TItem> Zone { get; set; } = default!;

        [CascadingParameter]
        GroveDropZoneContainer<TItem> Container { get; set; } = default!;

        void EnsurePayload(DragEventArgs? args = null)
        {
            Container.Payload = new GroveDropZoneItemEventArgs<TItem>()
            {
                FromZone = Zone,
                Item = Item,
                DataTransfer = args?.DataTransfer ?? default!
            };
        }

        async Task OnDragStart()
        {
            dragCssClass = "rz-dragging";
            EnsurePayload();

            if (Container != null)
            {
                await Container.DragStart.InvokeAsync(Container.Payload);
            }
        }

        void OnDragOver(DragEventArgs args)
        {
            if (Container?.Payload == null)
            {
                EnsurePayload(args);
            }

            if (Container?.Payload != null)
            {
                Container.Payload.ToItem = Item;
            }

            var canDrop = Zone?.CanDrop() ?? false;
            args.DataTransfer.DropEffect = canDrop ? "move" : "none";
            cssClass = canDrop ? "rz-can-drop" : "rz-no-drop";

            Zone?.OnDragOver(args);
        }

        void OnDragLeave(DragEventArgs args)
        {
            cssClass = "";
        }

        async Task OnDragEnd(DragEventArgs args)
        {
            dragCssClass = "";

            if (Container != null)
            {
                await Container.DragEnd.InvokeAsync(Container.Payload);
            }
        }

        async Task OnDrop(DragEventArgs args)
        {
            if (Container?.Payload == null)
            {
                EnsurePayload(args);
            }
            cssClass = "";
            if (Container?.Payload != null)
            {
                Container.Payload.ToItem = Item;
            }
            if (Zone != null)
            {
                await Zone.OnDropInternal();
            }
        }

        string cssClass = string.Empty;
        string dragCssClass = string.Empty;

        /// <inheritdoc />
        protected override string GetComponentCssClass()
        {
            return $"rz-dropzone-item {cssClass} {dragCssClass}".Trim();
        }
    }
}
