namespace Grove.Components.Primitives;

/// <summary>
/// Supplies information about a <see cref="Grove.Components.Primitives.GroveGoogleMap.MapClick" /> event that is being raised.
/// </summary>
public class GoogleMapClickEventArgs
{
    /// <summary>
    /// The position which represents the clicked map location.
    /// </summary>
    public GoogleMapPosition? Position { get; set; }
}

