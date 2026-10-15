namespace Grove.Display.Management.Implementation;

/// <summary>The displaying and processing events' context: the shape being mapped to the display tree.</summary>
public class ShapeDisplayContext
{
    public IShape Shape { get; set; }
    public IServiceProvider ServiceProvider { get; set; }
}
