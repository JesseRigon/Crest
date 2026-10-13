using System.Globalization;
using System.Text.Json;
using Crest.Regions.Models;
using NetTopologySuite.Geometries;

namespace Crest.Regions.Services;

/// <summary>
/// Postal-code files for the tree (docs/regions.md › Populating the tree): the loader only, no
/// rows ship, since which sources Crest may bundle is an open decision. Two shapes:
/// <list type="bullet">
/// <item>A table with a header row (tab or comma separated): <c>Country</c>, <c>Code</c>,
/// <c>NodeIds</c> (';'-separated), <c>Latitude</c>, <c>Longitude</c>.</item>
/// <item>The GeoNames postal-code dump (tab separated, no header: country, postal code, place,
/// admin1 name, admin1 code, admin2 name, admin2 code, admin3 name, admin3 code, latitude,
/// longitude, accuracy). Its admin codes name candidate nodes <c>{CC}-{admin1}</c> and
/// <c>{CC}-{admin1}-{admin2}</c>; the importer keeps only those the tree has.</item>
/// </list>
/// A code listed on several rows (a ZIP with several place names) becomes one record with
/// the union of its nodes and the first row's point. Codes are stored the way lookups
/// normalize them: trimmed, upper case, without spaces.
/// </summary>
public static class PostalCodeFileParser
{
    public const string TableFormat = "table";
    public const string GeoNamesFormat = "geonames";

    public static string NormalizeCode(string code) => code.Trim().ToUpperInvariant().Replace(" ", string.Empty, StringComparison.Ordinal);

    public static IReadOnlyList<GeoPostalCode> Read(TextReader reader, string format)
    {
        var merged = new Dictionary<(string Country, string Code), GeoPostalCode>();

        foreach (var row in string.Equals(format, GeoNamesFormat, StringComparison.OrdinalIgnoreCase) ? ReadGeoNames(reader) : ReadTable(reader))
        {
            if (string.IsNullOrWhiteSpace(row.Country) || string.IsNullOrWhiteSpace(row.Code))
            {
                continue;
            }

            row.Country = row.Country.Trim().ToUpperInvariant();
            row.Code = NormalizeCode(row.Code);

            if (merged.TryGetValue((row.Country, row.Code), out var existing))
            {
                existing.NodeIds = [.. existing.NodeIds.Union(row.NodeIds, StringComparer.Ordinal)];
            }
            else
            {
                merged[(row.Country, row.Code)] = row;
            }
        }

        return [.. merged.Values];
    }

    private static IEnumerable<GeoPostalCode> ReadTable(TextReader reader)
    {
        var header = reader.ReadLine();
        if (header is null)
        {
            yield break;
        }

        var separator = header.Contains('\t') ? '\t' : ',';
        var columns = header.Split(separator).Select(column => column.Trim()).ToArray();
        int Column(string name) => Array.FindIndex(columns, column => string.Equals(column, name, StringComparison.OrdinalIgnoreCase));
        var country = Column("Country");
        var code = Column("Code");
        var nodes = Column("NodeIds");
        var latitude = Column("Latitude");
        var longitude = Column("Longitude");
        if (country < 0 || code < 0)
        {
            throw new InvalidDataException("A postal-code table needs Country and Code columns (NodeIds, Latitude and Longitude are optional).");
        }

        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var cells = line.Split(separator);
            yield return new GeoPostalCode
            {
                Country = Cell(cells, country) ?? string.Empty,
                Code = Cell(cells, code) ?? string.Empty,
                NodeIds = (Cell(cells, nodes) ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
                Latitude = Number(cells, latitude),
                Longitude = Number(cells, longitude),
            };
        }
    }

    private static IEnumerable<GeoPostalCode> ReadGeoNames(TextReader reader)
    {
        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var cells = line.Split('\t');
            if (cells.Length < 11)
            {
                throw new InvalidDataException("Not a GeoNames postal-code file: each row has at least 11 tab-separated columns.");
            }

            var country = cells[0].Trim().ToUpperInvariant();
            var admin1 = cells[4].Trim();
            var admin2 = cells[6].Trim();
            var nodes = new List<string>();
            if (admin1.Length > 0)
            {
                nodes.Add($"{country}-{admin1}");
                if (admin2.Length > 0)
                {
                    nodes.Add($"{country}-{admin1}-{admin2}");
                }
            }

            yield return new GeoPostalCode
            {
                Country = country,
                Code = cells[1],
                NodeIds = nodes,
                Latitude = Number(cells, 9),
                Longitude = Number(cells, 10),
            };
        }
    }

    private static string? Cell(string[] cells, int index) => index >= 0 && index < cells.Length ? cells[index].Trim() : null;

    private static double? Number(string[] cells, int index) =>
        Cell(cells, index) is { Length: > 0 } text && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
}

/// <summary>
/// Boundaries from a GeoJSON FeatureCollection (docs/regions.md › Populating the tree): each
/// feature's Polygon or MultiPolygon becomes a <see cref="GeoBoundary"/> stored as a WKT
/// MultiPolygon (the built-in resolver reads WKT), keyed by a node id read from a feature
/// property, optionally through a format (<c>US-IL-{0}</c>). No rows ship.
/// </summary>
public static class GeoJsonBoundaryParser
{
    private static readonly GeometryFactory Factory = new();

    /// <param name="idProperty">The feature property holding the node id (or its local part).</param>
    /// <param name="idFormat">A composite format applied to the property's value; null uses it as is.</param>
    /// <param name="skipped">Features that were not read, with the reason.</param>
    public static IReadOnlyList<GeoBoundary> Read(Stream stream, string idProperty, string? idFormat, ICollection<string> skipped)
    {
        using var document = JsonDocument.Parse(stream);
        if (!document.RootElement.TryGetProperty("features", out var features) || features.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("Not a GeoJSON FeatureCollection: a features array is required.");
        }

        var boundaries = new List<GeoBoundary>();
        var index = 0;
        foreach (var feature in features.EnumerateArray())
        {
            index++;
            if (!feature.TryGetProperty("properties", out var properties)
                || !properties.TryGetProperty(idProperty, out var idValue)
                || idValue.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                skipped.Add($"Feature {index} has no '{idProperty}' property.");
                continue;
            }

            var local = idValue.ValueKind == JsonValueKind.String ? idValue.GetString()! : idValue.GetRawText();
            var nodeId = string.IsNullOrEmpty(idFormat) ? local : string.Format(CultureInfo.InvariantCulture, idFormat, local);

            if (!feature.TryGetProperty("geometry", out var geometry) || ToMultiPolygon(geometry) is not { } multiPolygon)
            {
                skipped.Add($"Feature {index} ({nodeId}) has no Polygon or MultiPolygon geometry.");
                continue;
            }

            boundaries.Add(new GeoBoundary { NodeId = nodeId, Format = "WKT", Geometry = multiPolygon.AsText() });
        }

        return boundaries;
    }

    private static MultiPolygon? ToMultiPolygon(JsonElement geometry)
    {
        if (geometry.ValueKind != JsonValueKind.Object
            || !geometry.TryGetProperty("type", out var type)
            || !geometry.TryGetProperty("coordinates", out var coordinates))
        {
            return null;
        }

        return type.GetString() switch
        {
            "Polygon" => Factory.CreateMultiPolygon([Polygon(coordinates)]),
            "MultiPolygon" => Factory.CreateMultiPolygon(coordinates.EnumerateArray().Select(Polygon).ToArray()),
            _ => null,
        };
    }

    private static Polygon Polygon(JsonElement rings)
    {
        var linear = rings.EnumerateArray().Select(ring => Factory.CreateLinearRing(ring.EnumerateArray()
            .Select(position => new Coordinate(position[0].GetDouble(), position[1].GetDouble()))
            .ToArray())).ToArray();
        return Factory.CreatePolygon(linear[0], linear[1..]);
    }
}
