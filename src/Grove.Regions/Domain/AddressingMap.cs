namespace Grove.Regions.Models;

/// <summary>What one level is called in one country and how it is entered.</summary>
public sealed class AddressingLevel
{
    public int Level { get; set; }
    public string Label { get; set; } = string.Empty;
    /// <summary>True: pick from the tree's nodes at this level. False: free text.</summary>
    public bool IsList { get; set; }
    public bool Required { get; set; }
    /// <summary>Whether the address FORM shows this level at all (a level can exist for tax and be invisible to entry).</summary>
    public bool OnForm { get; set; } = true;
}

public static class AddressingFields
{
    public const string Line1 = "Line1";
    public const string Line2 = "Line2";
    public const string Locality = "Locality";
    public const string PostalCode = "PostalCode";
    /// <summary>Prefix for a level field: "Level2", "Level3"...</summary>
    public const string LevelPrefix = "Level";

    public static bool IsLevel(string field, out int level)
    {
        level = 0;
        return field.StartsWith(LevelPrefix, StringComparison.Ordinal) && int.TryParse(field.AsSpan(LevelPrefix.Length), out level);
    }
}

/// <summary>Form field order and labels for one country.</summary>
public sealed class AddressingField
{
    /// <summary>One of <see cref="AddressingFields"/> or "Level{n}".</summary>
    public string Field { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Order { get; set; }
    public bool Required { get; set; }
}

/// <summary>The per-country addressing map: the pattern for both the UI and validation. Global-store reference data.</summary>
public sealed class AddressingMap
{
    public long Id { get; set; }
    public string Country { get; set; } = string.Empty;
    public List<AddressingLevel> Levels { get; set; } = [];
    public List<AddressingField> Fields { get; set; } = [];
    public string? PostalCodePattern { get; set; }
    public string PostalCodeLabel { get; set; } = "Postal code";
    /// <summary>Which level the free-text locality corresponds to, when the country treats city as a level (US 4). Null when locality is text only.</summary>
    public int? LocalityLevel { get; set; }

    /// <summary>
    /// True once the Default tenant edited the map at runtime (docs/regions.md › Addressing maps
    /// as runtime rules): a reload of the shipped data files leaves an edited map alone, and
    /// reverting it restores the shipped one.
    /// </summary>
    public bool Edited { get; set; }
}

/// <summary>Structural checks on an addressing map before it is stored; shared by server and client.</summary>
public static class AddressingMapValidator
{
    public static IReadOnlyList<string> Validate(AddressingMap map)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(map.Country) || (map.Country != "*" && (map.Country.Length != 2 || !map.Country.All(char.IsAsciiLetterUpper))))
        {
            errors.Add("The country must be an upper-case ISO 3166-1 alpha-2 code, or * for the generic map.");
        }

        if (map.Levels.Count == 0 || map.Levels.All(level => level.Level != 1))
        {
            errors.Add("A map declares level 1, the country.");
        }

        foreach (var duplicate in map.Levels.GroupBy(level => level.Level).Where(group => group.Count() > 1))
        {
            errors.Add($"Level {duplicate.Key} is declared more than once.");
        }

        if (map.Levels.Any(level => level.Level < 1))
        {
            errors.Add("Levels are numbered from 1.");
        }

        foreach (var duplicate in map.Fields.GroupBy(field => field.Field, StringComparer.Ordinal).Where(group => group.Count() > 1))
        {
            errors.Add($"The field {duplicate.Key} appears more than once.");
        }

        foreach (var field in map.Fields)
        {
            var known = field.Field is AddressingFields.Line1 or AddressingFields.Line2 or AddressingFields.Locality or AddressingFields.PostalCode;
            if (AddressingFields.IsLevel(field.Field, out var level))
            {
                known = map.Levels.Any(definition => definition.Level == level);
                if (!known)
                {
                    errors.Add($"The field {field.Field} names a level the map does not declare.");
                    continue;
                }
            }

            if (!known)
            {
                errors.Add($"Unknown field '{field.Field}': use Line1, Line2, Locality, PostalCode or Level{{n}}.");
            }
        }

        if (map.LocalityLevel is { } locality && map.Levels.All(level => level.Level != locality))
        {
            errors.Add($"The locality level {locality} is not declared.");
        }

        if (!string.IsNullOrWhiteSpace(map.PostalCodePattern))
        {
            try
            {
                _ = new System.Text.RegularExpressions.Regex(map.PostalCodePattern, System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromMilliseconds(250));
            }
            catch (ArgumentException exception)
            {
                errors.Add($"The postal-code pattern is not a valid regular expression: {exception.Message}");
            }
        }

        return errors;
    }
}
