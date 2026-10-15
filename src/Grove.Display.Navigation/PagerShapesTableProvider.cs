using Grove.Display.Management;
using Grove.Display.Management.Descriptors;
using Grove.Display.Management.Shapes;
using Grove.Display.Management.Utilities;

namespace Grove.Display.Navigation;

public class PagerShapesTableProvider : ShapeTableProvider
{
    public override ValueTask DiscoverAsync(ShapeTableBuilder builder)
    {
        builder.Describe("Pager")
            .OnCreated(created =>
            {
                // Initializes the common properties of a Pager shape
                // such that views can safely add values to them.
                created.Shape.Properties["ItemClasses"] = new List<string>();
                created.Shape.Properties["ItemAttributes"] = new Dictionary<string, string>();
            })
            .OnDisplaying(displaying =>
            {
                if (displaying.Shape.TryGetProperty("PagerId", out string pagerId) && !string.IsNullOrEmpty(pagerId))
                {
                    displaying.Shape.Metadata.Alternates.Add("Pager__" + pagerId.EncodeAlternateElement());
                }
            });

        builder.Describe("PagerSlim")
            .OnCreated(created =>
            {
                // Initializes the common properties of a Pager shape
                // such that views can safely add values to them.
                created.Shape.Properties["ItemClasses"] = new List<string>();
                created.Shape.Properties["ItemAttributes"] = new Dictionary<string, string>();
            })
            .OnDisplaying(displaying =>
            {
                if (displaying.Shape.TryGetProperty("PagerId", out string pagerId) && !string.IsNullOrEmpty(pagerId))
                {
                    displaying.Shape.Metadata.Alternates.Add("Pager__" + pagerId.EncodeAlternateElement());
                }
            });

        builder.Describe("Pager_Gap")
            .OnDisplaying(displaying =>
            {
                var shape = displaying.Shape;

                if (shape.TryGetProperty("Pager", out IShape pager) && pager.TryGetProperty("PagerId", out string pagerId) && !string.IsNullOrEmpty(pagerId))
                {
                    displaying.Shape.Metadata.Alternates.Add("Pager_Gap__" + pagerId.EncodeAlternateElement());
                }
            });

        builder.Describe("Pager_First")
            .OnDisplaying(displaying =>
            {
                var shape = displaying.Shape;

                if (shape.TryGetProperty("Pager", out IShape pager) && pager.TryGetProperty("PagerId", out string pagerId) && !string.IsNullOrEmpty(pagerId))
                {
                    displaying.Shape.Metadata.Alternates.Add("Pager_First__" + pagerId.EncodeAlternateElement());
                }
            });

        builder.Describe("Pager_Previous")
            .OnDisplaying(displaying =>
            {
                var shape = displaying.Shape;

                if (shape.TryGetProperty("Pager", out IShape pager) && pager.TryGetProperty("PagerId", out string pagerId) && !string.IsNullOrEmpty(pagerId))
                {
                    displaying.Shape.Metadata.Alternates.Add("Pager_Previous__" + pagerId.EncodeAlternateElement());
                }
            });

        builder.Describe("Pager_Next")
            .OnDisplaying(displaying =>
            {
                var shape = displaying.Shape;

                if (shape.TryGetProperty("Pager", out IShape pager) && pager.TryGetProperty("PagerId", out string pagerId) && !string.IsNullOrEmpty(pagerId))
                {
                    displaying.Shape.Metadata.Alternates.Add("Pager_Next__" + pagerId.EncodeAlternateElement());
                }
            });

        builder.Describe("Pager_Last")
            .OnDisplaying(displaying =>
            {
                var shape = displaying.Shape;

                if (shape.TryGetProperty("Pager", out IShape pager) && pager.TryGetProperty("PagerId", out string pagerId) && !string.IsNullOrEmpty(pagerId))
                {
                    displaying.Shape.Metadata.Alternates.Add("Pager_Last__" + pagerId.EncodeAlternateElement());
                }
            });

        builder.Describe("Pager_CurrentPage")
            .OnDisplaying(displaying =>
            {
                var shape = displaying.Shape;

                if (shape.TryGetProperty("Pager", out IShape pager) && pager.TryGetProperty("PagerId", out string pagerId) && !string.IsNullOrEmpty(pagerId))
                {
                    displaying.Shape.Metadata.Alternates.Add("Pager_CurrentPage__" + pagerId.EncodeAlternateElement());
                }
            });

        builder.Describe("Pager_Links")
            .OnDisplaying(displaying =>
            {
                if (displaying.Shape.TryGetProperty("PagerId", out string pagerId))
                {
                    displaying.Shape.Metadata.Alternates.Add("Pager_Links__" + pagerId.EncodeAlternateElement());
                }
            });

        return ValueTask.CompletedTask;
    }
}
