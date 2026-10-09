# Crest.Iconify

`Crest.Iconify` owns the Iconify-specific integration layer for Crest Crest UI Framework.

It provides:

- C# provider settings for Iconify-compatible APIs;
- local full-library mirror contracts and implementation;
- install/first-run full-library cache sync from `https://github.com/iconify/icon-sets.git`.

It does not depend on `Crest.Icons`. The dependency direction is `Crest.Icons -> Crest.Iconify`.

The local full-library cache setting is exposed in C# as `IconifyIconProviderSettings.LocalLibraryCacheEnabled` and defaults to `true`. UI for changing this setting is intentionally not exposed yet.

The NuGet package intentionally does not include Iconify's full JSON data. On the target machine, install/first-run sync populates `App_Data/Crest/Icons/IconifyCache` from GitHub so the package stays small.
