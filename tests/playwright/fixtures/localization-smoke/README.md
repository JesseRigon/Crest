# Localization smoke-test fixtures

Reference copies of the `.po` entries that back `checks/localization-smoke-site.js`. The
files actually consumed at runtime live at
`Crest.SiteTheme/Localization/{culture}.po` — `ModularPoFileLocationProvider`
resolves each extension's own `Localization/` folder first (see
`docs/localization.md`), so `Crest.SiteTheme` ships its own test
string ("Welcome") without needing a global `/Localization/{culture}/*.po` entry.

These copies exist so the test suite has its own stable reference to what the check
expects, independent of the theme project's files — if a future change to the theme's
`.po` files breaks the check, diffing against this directory shows whether the fixture
itself or the theme's copy drifted. Keep both copies in sync by hand; there's no build
step that generates one from the other.

Add more entries here (and mirror them into the theme's `Localization/` folder) if
`localization-smoke-site.js` grows to check additional strings.
