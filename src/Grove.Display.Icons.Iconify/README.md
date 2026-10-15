# Grove.Display.Icons.Iconify

`Grove.Display.Icons.Iconify` owns the Iconify-specific integration layer for Grove Grove UI Framework.

It provides:

- C# provider settings for Iconify-compatible APIs;
- local full-library mirror contracts and implementation;
- install/first-run full-library cache sync from `https://github.com/iconify/icon-sets.git`.

It does not depend on `Grove.Display.Icons`. The dependency direction is `Grove.Display.Icons -> Grove.Display.Icons.Iconify`.

The local full-library cache setting is exposed in C# as `IconifyIconProviderSettings.LocalLibraryCacheEnabled` and defaults to `true`. UI for changing this setting is intentionally not exposed yet.

The NuGet package intentionally does not include Iconify's full JSON data. On the target machine, install/first-run sync populates `App_Data/Grove/Icons/IconifyCache` from GitHub so the package stays small.
