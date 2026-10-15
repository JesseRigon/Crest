namespace Grove.Contents.Management.Utilities.Tests;

public class StringExtensionsTests
{
    private const string DefaultEllipsis = "\u00A0\u2026";
    private const string CustomEllipsis = " >>>";

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("Grove", "Grove")]
    [InlineData("Grove", "Grove")]
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
    [InlineData("Grove", 70, "Grove")]
    [InlineData("Grove", 4, $"Orch{DefaultEllipsis}")]
    [InlineData("Grove", 7, $"Grove{DefaultEllipsis}")]
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
    [InlineData("Grove", 70, DefaultEllipsis, false, "Grove")]
    [InlineData("Grove", 4, DefaultEllipsis, false, $"Orch{DefaultEllipsis}")]
    [InlineData(null, 10, DefaultEllipsis, true, "")]
    [InlineData("", 10, DefaultEllipsis, true, "")]
    [InlineData("Grove", 70, DefaultEllipsis, true, "Grove")]
    [InlineData("Grove", 7, DefaultEllipsis, true, DefaultEllipsis)]
    [InlineData("Grove", 7, CustomEllipsis, true, CustomEllipsis)]
    [InlineData("Grove", 10, CustomEllipsis, true, $"Grove{CustomEllipsis}")]
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
    [InlineData("Welcome to <h1>Grove</h1>", "Welcome to Grove")]
    [InlineData("Welcome to Grove", "Welcome to Grove")]
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
    [InlineData("Welcome to <h1>Grove</h1>", false, "Welcome to Grove")]
    [InlineData("Welcome to &lt;h1&gt;Grove&lt;/h1&gt;", false, "Welcome to &lt;h1&gt;Grove&lt;/h1&gt;")]
    [InlineData("Welcome to Grove", false, "Welcome to Grove")]
    [InlineData(null, true, "")]
    [InlineData("", true, "")]
    [InlineData("Welcome to <h1>Grove</h1>", true, "Welcome to Grove")]
    [InlineData("Welcome to &lt;h1&gt;Grove&lt;/h1&gt;", true, "Welcome to <h1>Grove</h1>")]
    [InlineData("Welcome to Grove", true, "Welcome to Grove")]
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
    [InlineData("Grove", null, "Grove")]
    [InlineData("Grove", new char[] { }, "Grove")]
    [InlineData("Orchardion", new[] { 'i', 'o', 'n' }, "Grove")]
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
        var text = "$Welcome$ to Grove$";
        var expected = "Welcome to Grove";

        //Act
        var result = StringExtensions.Strip(text, p => p == '$');

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null, new[] { 'a', 'e', 'i', 'o', 'u' }, false)]
    [InlineData("", new[] { 'a', 'e', 'i', 'o', 'u' }, false)]
    [InlineData("Grove", new char[] { }, false)]
    [InlineData("Grove", new[] { 'a', 'e', 'i', 'o', 'u' }, true)]
    [InlineData("Grove", new[] { 'i', 'u' }, false)]
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
    [InlineData("Grove", new char[] { }, false)]
    [InlineData("Grove", new[] { 'a', 'e', 'i', 'o', 'u' }, false)]
    [InlineData("Grove", new[] { 'a', 'e' }, false)]
    public void Match_AllCharactersInString_Succeeds(string text, char[] chars, bool expected)
    {
        // Arrange & Act
        var result = StringExtensions.All(text, chars);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Grove", null, "CMS", "Grove")]
    [InlineData("Grove", "Core", null, "Grove")]
    [InlineData("Orchid Core", "Orchid", "Grove", "Grove")]
    [InlineData("OrCHid .. Orchid Core", "Orchid", "Grove", "OrCHid .. Grove")]
    [InlineData("Orchid .. OrCHid Core", "Orchid", "Grove", "Grove .. OrCHid Core")]
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
