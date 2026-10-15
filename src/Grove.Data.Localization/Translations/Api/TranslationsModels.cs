#nullable enable

using System.Globalization;
using Grove.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Grove.Data.Localization.Translations.Models;
using Grove.Data.Localization.Translations.Services;
using Grove.Data.Localization;
using Grove.Data.Localization.Data;

namespace Grove.Data.Localization.Translations.Api;

public sealed record TranslationsModel(
    TranslationCulture[] Cultures,
    string Culture,
    bool CanEdit,
    TranslationGroup[] Groups);

public sealed record TranslationCulture(string Value, string Label, bool CanEdit);

public sealed record TranslationGroup(string Name, TranslationString[] Strings);

/// <param name="Orphan">A stored entry no provider currently enumerates - e.g. its feature is
/// disabled, or its source string changed. Still applied at render time by IDataLocalizer;
/// editable and deletable here like any other row.</param>
/// <param name="Fallback">For rows without a stored value: the shipped PO translation that
/// actually renders (resolution is store edit -> PO -> invariant literal), shown as the
/// input's placeholder. Deleting a stored value reveals this layer rather than the raw
/// literal; saving the key itself as the value pins the literal over it.</param>
public sealed record TranslationString(string Context, string Key, string Value, bool Orphan, string? Fallback = null);

public sealed record TranslationsSaveModel([property: NotEmpty] string Culture, TranslationSaveEntry[]? Translations);

public sealed record TranslationSaveEntry(string Context, string Key, string? Value);
