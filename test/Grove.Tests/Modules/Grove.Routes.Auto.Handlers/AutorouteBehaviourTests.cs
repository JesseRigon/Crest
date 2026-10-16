using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.Extensions.Localization;
using Moq;
using Grove.Routes.Auto.Services;
using Grove.Routes.Auto.Models;
using Grove.Contents.Management.Routing;
using System.Threading.Tasks;
using Xunit;

namespace Grove.Tests.Modules.Grove.Autoroute.Handlers;

public class AutorouteBehaviourTests
{
    [Fact]
    public async Task GenerateRelativeUniquePathAsync_ShouldReturnUniquePath_WhenPathAlreadyExists()
    {
        // Arrange
        var expectedPath = "existing-path";
        var contextContentItemId = "context-item-id";
        var existingEntries = new List<AutorouteEntry>
        {
            new AutorouteEntry("id", expectedPath, "other-item-id"),
            new AutorouteEntry("id", $"{expectedPath}-1", "other-item-id"),
        };

        var context = new AutorouteFieldSet
        {
            ContentItem = new global::Grove.Contents.Management.ContentItem
            {
                ContentItemId = contextContentItemId,
            },
        };

        // Act
        var actualPath = AutorouteBehaviour.GenerateRelativeUniquePath(existingEntries, expectedPath, context);

        // Assert
        Assert.Equal($"{expectedPath}-2", actualPath);
    }
}

public class AutorouteFieldSetExtensionsTests
{
    private readonly Mock<IStringLocalizer> _localizerMock = new();

    [Fact]
    public void ValidatePathFieldValue_ReturnsError_WhenPathIsHomepage()
    {
        // Arrange
        var part = new AutorouteFieldSet { Path = "/" };
        _localizerMock.Setup(l => l["Your permalink can't be set to the homepage, please use the homepage option instead."])
            .Returns(new LocalizedString("Your permalink can't be set to the homepage, please use the homepage option instead.", "Your permalink can't be set to the homepage, please use the homepage option instead."));

        // Act
        var results = part.ValidatePathFieldValue(_localizerMock.Object).ToList();

        // Assert
        Assert.Single(results);
        Assert.Equal(nameof(part.Path), results[0].MemberNames.Single());
    }

    [Theory]
    [InlineData("test:path")]
    [InlineData("test?path")]
    [InlineData("test#path")]
    [InlineData("test[path")]
    [InlineData("test]path")]
    [InlineData("test@path")]
    [InlineData("test!path")]
    [InlineData("test$path")]
    [InlineData("test&path")]
    [InlineData("test'path")]
    [InlineData("test(path")]
    [InlineData("test)path")]
    [InlineData("test*path")]
    [InlineData("test+path")]
    [InlineData("test,path")]
    [InlineData("test.path")]
    [InlineData("test;path")]
    [InlineData("test=path")]
    [InlineData("test<path")]
    [InlineData("test>path")]
    [InlineData("test\\path")]
    [InlineData("test|path")]
    [InlineData("test%path")]
    public void ValidatePathFieldValue_ReturnsError_WhenPathContainsInvalidCharacters(string invalidPath)
    {
        // Arrange
        var part = new AutorouteFieldSet { Path = invalidPath };
        var invalidChars = string.Join(", ", AutorouteFieldSet.InvalidCharactersForPath.Select(c => $"\"{c}\""));
        _localizerMock.Setup(l => l[It.Is<string>(s => s.Contains("Please do not use any of the following characters"))])
            .Returns(new LocalizedString($"Please do not use any of the following characters in your permalink: {invalidChars}. No spaces, or consecutive slashes, are allowed (please use dashes or underscores instead).", $"Please do not use any of the following characters in your permalink: {invalidChars}. No spaces, or consecutive slashes, are allowed (please use dashes or underscores instead)."));

        // Act
        var results = part.ValidatePathFieldValue(_localizerMock.Object).ToList();

        // Assert
        Assert.Single(results);
        Assert.Equal(nameof(part.Path), results[0].MemberNames.Single());
    }

    [Fact]
    public void ValidatePathFieldValue_ReturnsError_WhenPathContainsSpace()
    {
        // Arrange
        var part = new AutorouteFieldSet { Path = "test path" };
        var invalidChars = string.Join(", ", AutorouteFieldSet.InvalidCharactersForPath.Select(c => $"\"{c}\""));
        _localizerMock.Setup(l => l[It.Is<string>(s => s.Contains("Please do not use any of the following characters"))])
            .Returns(new LocalizedString($"Please do not use any of the following characters in your permalink: {invalidChars}. No spaces, or consecutive slashes, are allowed (please use dashes or underscores instead).", $"Please do not use any of the following characters in your permalink: {invalidChars}. No spaces, or consecutive slashes, are allowed (please use dashes or underscores instead)."));

        // Act
        var results = part.ValidatePathFieldValue(_localizerMock.Object).ToList();

        // Assert
        Assert.Single(results);
    }

    [Fact]
    public void ValidatePathFieldValue_ReturnsError_WhenPathContainsConsecutiveSlashes()
    {
        // Arrange
        var part = new AutorouteFieldSet { Path = "test//path" };
        var invalidChars = string.Join(", ", AutorouteFieldSet.InvalidCharactersForPath.Select(c => $"\"{c}\""));
        _localizerMock.Setup(l => l[It.Is<string>(s => s.Contains("Please do not use any of the following characters"))])
            .Returns(new LocalizedString($"Please do not use any of the following characters in your permalink: {invalidChars}. No spaces, or consecutive slashes, are allowed (please use dashes or underscores instead).", $"Please do not use any of the following characters in your permalink: {invalidChars}. No spaces, or consecutive slashes, are allowed (please use dashes or underscores instead)."));

        // Act
        var results = part.ValidatePathFieldValue(_localizerMock.Object).ToList();

        // Assert
        Assert.Single(results);
    }

    [Fact]
    public void ValidatePathFieldValue_ReturnsError_WhenPathExceedsMaxLength()
    {
        // Arrange
        var part = new AutorouteFieldSet { Path = new string('a', AutorouteFieldSet.MaxPathLength + 1) };
        _localizerMock.Setup(l => l[It.Is<string>(s => s.Contains("Your permalink is too long"))])
            .Returns(new LocalizedString($"Your permalink is too long. The permalink can only be up to {AutorouteFieldSet.MaxPathLength} characters.", $"Your permalink is too long. The permalink can only be up to {AutorouteFieldSet.MaxPathLength} characters."));

        // Act
        var results = part.ValidatePathFieldValue(_localizerMock.Object).ToList();

        // Assert
        Assert.Single(results);
        Assert.Equal(nameof(part.Path), results[0].MemberNames.Single());
    }

    [Fact]
    public void ValidatePathFieldValue_ReturnsNoErrors_WhenPathIsValid()
    {
        // Arrange
        var part = new AutorouteFieldSet { Path = "valid-path" };

        // Act
        var results = part.ValidatePathFieldValue(_localizerMock.Object).ToList();

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void ValidatePathFieldValue_ReturnsNoErrors_WhenPathIsEmpty()
    {
        // Arrange
        var part = new AutorouteFieldSet { Path = "" };

        // Act
        var results = part.ValidatePathFieldValue(_localizerMock.Object).ToList();

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void ValidatePathFieldValue_ReturnsNoErrors_WhenPathIsNull()
    {
        // Arrange
        var part = new AutorouteFieldSet { Path = null };

        // Act
        var results = part.ValidatePathFieldValue(_localizerMock.Object).ToList();

        // Assert
        Assert.Empty(results);
    }
}