using System.Text;
using Crest.Regions.Global;
using Crest.Regions.Models;
using Crest.Regions.Services;
using Xunit;

namespace Crest.Regions.Tests;

// docs/regions.md › Populating the tree (the loaders; no rows ship) and › Addressing maps as
// runtime rules (the edit checks and the shipped map a revert restores).
public class GeoDataLoadingTests
{
    [Fact]
    public void A_postal_table_merges_rows_of_one_code_and_normalizes_it()
    {
        const string file = "Country,Code,NodeIds,Latitude,Longitude\nUS,60601,US-IL;US-IL-031,41.88,-87.62\nus,60601,US-IL-043,,\ngb,sw1a 1aa,GB-ENG,51.5,-0.14\n";
        var codes = PostalCodeFileParser.Read(new StringReader(file), PostalCodeFileParser.TableFormat);

        var chicago = Assert.Single(codes, code => code.Country == "US" && code.Code == "60601");
        Assert.Equal(["US-IL", "US-IL-031", "US-IL-043"], chicago.NodeIds);
        Assert.Equal(41.88, chicago.Latitude);
        Assert.Contains(codes, code => code.Country == "GB" && code.Code == "SW1A1AA");
    }

    [Fact]
    public void A_geonames_row_names_its_state_and_county_nodes()
    {
        const string file = "US\t60601\tChicago\tIllinois\tIL\tCook\t031\t\t\t41.8858\t-87.6181\t4\n";
        var code = Assert.Single(PostalCodeFileParser.Read(new StringReader(file), PostalCodeFileParser.GeoNamesFormat));

        Assert.Equal(["US-IL", "US-IL-031"], code.NodeIds);
        Assert.Equal(-87.6181, code.Longitude);
    }

    [Fact]
    public void A_geojson_polygon_becomes_a_wkt_multipolygon_keyed_by_its_formatted_id()
    {
        const string json = """
            {"type":"FeatureCollection","features":[
              {"type":"Feature","properties":{"COUNTYFP":"031"},"geometry":{"type":"Polygon","coordinates":[[[-88,41],[-87,41],[-87,42],[-88,42],[-88,41]]]}},
              {"type":"Feature","properties":{},"geometry":{"type":"Polygon","coordinates":[[[0,0],[1,0],[1,1],[0,0]]]}}
            ]}
            """;
        var skipped = new List<string>();
        var boundary = Assert.Single(GeoJsonBoundaryParser.Read(new MemoryStream(Encoding.UTF8.GetBytes(json)), "COUNTYFP", "US-IL-{0}", skipped));

        Assert.Equal("US-IL-031", boundary.NodeId);
        Assert.StartsWith("MULTIPOLYGON", boundary.Geometry, StringComparison.Ordinal);
        Assert.Single(skipped);
    }

    [Fact]
    public void The_validator_refuses_an_unknown_level_field_and_a_bad_pattern()
    {
        var map = new AddressingMap
        {
            Country = "US",
            Levels = [new AddressingLevel { Level = 1, Label = "Country" }],
            Fields = [new AddressingField { Field = "Level2", Label = "State" }],
            PostalCodePattern = "([0-9",
        };

        var errors = AddressingMapValidator.Validate(map);

        Assert.Contains(errors, error => error.Contains("Level2", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("regular expression", StringComparison.Ordinal));
    }

    [Fact]
    public void The_shipped_us_map_is_valid_and_what_a_revert_restores()
    {
        var us = GeoGlobalSchema.ShippedMap("US");

        Assert.NotNull(us);
        Assert.False(us.Edited);
        Assert.Empty(AddressingMapValidator.Validate(us));
    }
}
