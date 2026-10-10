# Crest.Icons

`Crest.Icons` owns icon infrastructure for Crest Crest UI Framework:

- icon provider contracts and settings;
- icon registry/search/pack services;
- provider adapters, including the default Iconify adapter through `Crest.Iconify`;
- tenant media icon support;
- icon UI such as `IconSelector`;
- icon-specific CSS, JavaScript, and static assets.

This module depends on `Crest.Iconify` for Iconify-specific provider APIs and may depend on `Crest.Components` for shared Radzen-backed UI primitives.

`Crest.Components` must not depend on this module. Admin/Site themes reference both projects when they need icon UI compiled into the WASM app.

Icon-specific component CSS should live here, preferably under:

```text
wwwroot/CrestIcons.css
wwwroot/icons.js
```

Theme stylesheets should only contain intentional theme overrides.

## Local Iconify cache boundary

The full Iconify library cache belongs to `Crest.Iconify`. `Crest.Icons` owns the generic provider registry and smaller used-icon/client pack cache that applies across all providers.
