namespace Grove.Display.Management.Descriptors;

public class ShapeTable
{
    public ShapeTable(IDictionary<string, ShapeDescriptor> descriptors, IDictionary<string, ShapeBinding> bindings)
    {
        Descriptors = descriptors;
        Bindings = bindings;
    }

    public IDictionary<string, ShapeDescriptor> Descriptors { get; }
    public IDictionary<string, ShapeBinding> Bindings { get; }

    /// <summary>
    /// The descriptor of a shape type: the type as is, else its fundamental type (the part before
    /// the first <c>__</c>; a descriptor's type never contains the separator).
    /// </summary>
    public ShapeDescriptor GetShapeDescriptor(string shapeType)
    {
        if (!Descriptors.TryGetValue(shapeType, out var shapeDescriptor))
        {
            var index = shapeType.IndexOf("__", StringComparison.Ordinal);
            if (index > 0)
            {
                Descriptors.TryGetValue(shapeType[..index], out shapeDescriptor);
            }
        }

        return shapeDescriptor;
    }
}
