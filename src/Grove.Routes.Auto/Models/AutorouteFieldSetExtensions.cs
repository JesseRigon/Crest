using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Localization;

namespace Grove.Routes.Auto.Models;

public static class AutorouteFieldSetExtensions
{
    public static IEnumerable<ValidationResult> ValidatePathFieldValue(this AutorouteFieldSet autoroute, IStringLocalizer S)
    {
        if (autoroute.Path == "/")
        {
            yield return new ValidationResult(S["Your permalink can't be set to the homepage, please use the homepage option instead."], new[] { nameof(autoroute.Path) });
        }

        if (HasInvalidCharacters(autoroute.Path))
        {
            var invalidCharactersForMessage = string.Join(", ", AutorouteFieldSet.InvalidCharactersForPath.Select(c => $"\"{c}\""));
            yield return new ValidationResult(S["Please do not use any of the following characters in your permalink: {0}. No spaces, or consecutive slashes, are allowed (please use dashes or underscores instead).", invalidCharactersForMessage], new[] { nameof(autoroute.Path) });
        }

        if (autoroute.Path?.Length > AutorouteFieldSet.MaxPathLength)
        {
            yield return new ValidationResult(S["Your permalink is too long. The permalink can only be up to {0} characters.", AutorouteFieldSet.MaxPathLength], new[] { nameof(autoroute.Path) });
        }
    }

    private static bool HasInvalidCharacters(string path)
    {
        // IndexOfAny performs culture-insensitive and case-sensitive search.
        if (path?.IndexOfAny(AutorouteFieldSet.InvalidCharactersForPath) > -1)
        {
            return true;
        }

        if (path?.IndexOf(' ', StringComparison.Ordinal) > -1)
        {
            return true;
        }

        if (path?.IndexOf("//", StringComparison.Ordinal) > -1)
        {
            return true;
        }

        return false;
    }
}
