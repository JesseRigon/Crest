namespace Crest.ContentManagement.Utilities.Tests;

public class StringExtensionsTests
{
    private const string DefaultEllipsis = "\u00A0\u2026";
    private const string CustomEllipsis = " >>>";

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("Crest", "Crest")]
    [InlineData("Crest", "Crest")]
    [InlineData("platformCore", "orchard Core")]
    public void CamelFriendly_Default_ReturnsCamelCase(string value, string expected)
    {
        // Arrange & Act
        var result = StringExtensions.CamelFriendly(value);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null, 10, "")]
    [InlineData("", 10, "")]
    [InlineData("Crest", 70, "Crest")]
    [InlineData("Crest", 4, $"Orch{DefaultEllipsis}")]
    [InlineData("Crest", 7, $"Crest{DefaultEllipsis}")]
    public void Ellipsize_Default_TrimsString(string text, int characterCount, string expected)
    {
        // Arrange & Act
        var result = StringExtensions.Ellipsize(text, characterCount);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null, 10, DefaultEllipsis, false, "")]
    [InlineData("", 10, DefaultEllipsis, false, "")]
    [InlineData("Crest", 70, DefaultEllipsis, false, "Crest")]
    [InlineData("Crest", 4, DefaultEllipsis, false, $"Orch{DefaultEllipsis}")]
    [InlineData(null, 10, DefaultEllipsis, true, "")]
    [InlineData("", 10, DefaultEllipsis, true, "")]
    [InlineData("Crest", 70, DefaultEllipsis, true, "Crest")]
    [InlineData("Crest", 7, DefaultEllipsis, true, DefaultEllipsis)]
    [InlineData("Crest", 7, CustomEllipsis, true, CustomEllipsis)]
    [InlineData("Crest", 10, CustomEllipsis, true, $"Crest{CustomEllipsis}")]
    public void Ellipsize_CustomEllipsisString_TrimssString(string text, int characterCount, string ellipsis, bool wordBoundary, string expected)
    {
        // Arrange & Act
        var result = StringExtensions.Ellipsize(text, characterCount, ellipsis, wordBoundary);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("Welcome to <h1>Crest</h1>", "Welcome to Crest")]
    [InlineData("Welcome to Crest", "Welcome to Crest")]
    public void Remove_TagsFromString_Succeeds(string text, string expected)
    {
        // Arrange & Act
        var result = StringExtensions.RemoveTags(text);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null, false, "")]
    [InlineData("", false, "")]
    [InlineData("Welcome to <h1>Crest</h1>", false, "Welcome to Crest")]
    [InlineData("Welcome to &lt;h1&gt;Crest&lt;/h1&gt;", false, "Welcome to &lt;h1&gt;Crest&lt;/h1&gt;")]
    [InlineData("Welcome to Crest", false, "Welcome to Crest")]
    [InlineData(null, true, "")]
    [InlineData("", true, "")]
    [InlineData("Welcome to <h1>Crest</h1>", true, "Welcome to Crest")]
    [InlineData("Welcome to &lt;h1&gt;Crest&lt;/h1&gt;", true, "Welcome to <h1>Crest</h1>")]
    [InlineData("Welcome to Crest", true, "Welcome to Crest")]
    public void RemoveTagsFromString_HtmlDecode_Succeeds(string text, bool htmlEncode, string expected)
    {
        // Arrange & Act
        var result = StringExtensions.RemoveTags(text, htmlEncode);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null, new[] { 'i', 'o', 'n' }, null)]
    [InlineData("", new[] { 'i', 'o', 'n' }, "")]
    [InlineData("Crest", null, "Crest")]
    [InlineData("Crest", new char[] { }, "Crest")]
    [InlineData("Orchardion", new[] { 'i', 'o', 'n' }, "Crest")]
    public void Strip_CharactersFromString_Succeeds(string text, char[] strippedChars, string expected)
    {
        // Arrange & Act
        var result = StringExtensions.Strip(text, strippedChars);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Strip_CharactersFromStringByPredicate_Succeeds()
    {
        // Arrange
        var text = "$Welcome$ to Crest$";
        var expected = "Welcome to Crest";

        //Act
        var result = StringExtensions.Strip(text, p => p == '$');

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null, new[] { 'a', 'e', 'i', 'o', 'u' }, false)]
    [InlineData("", new[] { 'a', 'e', 'i', 'o', 'u' }, false)]
    [InlineData("Crest", new char[] { }, false)]
    [InlineData("Crest", new[] { 'a', 'e', 'i', 'o', 'u' }, true)]
    [InlineData("Crest", new[] { 'i', 'u' }, false)]
    public void Match_AnyCharacterInString_Succeeds(string text, char[] chars, bool expected)
    {
        // Arrange & Act
        var result = StringExtensions.Any(text, chars);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null, new[] { 'a', 'e', 'i', 'o', 'u' }, false)]
    [InlineData("", new[] { 'a', 'e', 'i', 'o', 'u' }, false)]
    [InlineData("Crest", new char[] { }, false)]
    [InlineData("Crest", new[] { 'a', 'e', 'i', 'o', 'u' }, false)]
    [InlineData("Crest", new[] { 'a', 'e' }, false)]
    public void Match_AllCharactersInString_Succeeds(string text, char[] chars, bool expected)
    {
        // Arrange & Act
        var result = StringExtensions.All(text, chars);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Crest", null, "CMS", "Crest")]
    [InlineData("Crest", "Core", null, "Crest")]
    [InlineData("Orchid Core", "Orchid", "Crest", "Crest")]
    [InlineData("OrCHid .. Orchid Core", "Orchid", "Crest", "OrCHid .. Crest")]
    [InlineData("Orchid .. OrCHid Core", "Orchid", "Crest", "Crest .. OrCHid Core")]
    public void Replace_LastOccuranceInString_Succeeds(string text, string searchedText, string replacedText, string expected)
    {
        // Arrange & Act
        var result = StringExtensions.ReplaceLastOccurrence(text, searchedText, replacedText);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Hisham Bin Ateya", "Hisham Bin Ateya")]
    [InlineData("Sébastien Ros", "Sebastien Ros")]
    [InlineData("Zoltán Lehóczky", "Zoltan Lehoczky")]
    public void Remove_DiacriticsFromString_Succeeds(string text, string expected)
    {
        // Arrange & Act
        var result = StringExtensions.RemoveDiacritics(text);

        // Assert
        Assert.Equal(expected, result);
    }
}
