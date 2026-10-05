# Third-party notices

Crest vendors and adapts several permissively licensed works. Each one keeps its own
licence file beside the code; this is the inventory.

## Vendored source

| Code | Upstream | Licence | Licence file |
| --- | --- | --- | --- |
| `Crest.Workflows/engine/` — the workflow engine (22 projects) | [elsa-workflows/elsa-core](https://github.com/elsa-workflows/elsa-core), tag 3.6.0 (`d4b69be`) | MIT, Copyright (c) 2021 Elsa Workflows | `Crest.Workflows/engine/LICENSE` |
| `Crest.Workflows/designer/` — the workflow designer | [elsa-workflows/elsa-studio](https://github.com/elsa-workflows/elsa-studio), tag 3.6.0 (`1e293c5`) | MIT, Copyright (c) 2023 Elsa Workflows | `Crest.Workflows/designer/LICENSE` |
| `Crest.Workflows/Server/`, `Crest.Workflows/Contents/`, `Crest.Workflows/reference/` — the Orchard integration | [elsa-workflows/elsa-orchard-core](https://github.com/elsa-workflows/elsa-orchard-core) | BSD-3-Clause, Copyright (c) 2019 Elsa Workflows | `Crest.Workflows/LICENSE` |

Every identifier, namespace and assembly in these trees was renamed to `Crest.Workflows.*`
(`Crest.Workflows/tools/rename-upstream.py` applies the mapping). The upstream commits are
pinned in `Crest.Workflows/engine/UPSTREAM-COMMIT` and
`Crest.Workflows/designer/UPSTREAM-COMMIT`; `Crest.Workflows/UPSTREAM.md` explains what the
rename costs an upstream merge.

## Adapted at file boundaries

| Code | Upstream | Licence | Notice |
| --- | --- | --- | --- |
| `Crest.Money/` — money value types, `MoneyService`, `PriceField` | [OrchardCore.Commerce](https://github.com/OrchardCMS/OrchardCore.Commerce) | MIT, Copyright (c) 2018 OrchardCMS | `Crest.Money/NOTICE.md` |

Each adapted file carries its attribution in its own header.

## Forked libraries

| Code | Upstream | Licence | Notice |
| --- | --- | --- | --- |
| `Crest.Components/` — the Blazor component library | [Radzen Blazor](https://github.com/radzenhq/radzen-blazor) | MIT, Copyright (c) 2018-2026 Radzen Ltd | `Crest.Components/NOTICE.md` |

The component library was forked from Radzen's source and renamed `Radzen*` → `Crest*`;
the chart, data grid, scheduler, Gantt, HTML editor, spreadsheet, tree, upload, form and
dropdown families all originate there, as do the models, services and JavaScript that
support them. Behaviour has since diverged and components with no Radzen counterpart were
added, but the library as a whole is a derivative work. `Crest.Admin` and
anything else built on it inherit that status.

## Libraries used as packages

Radzen Blazor, MudBlazor (in the workflow designer) and the rest of the NuGet and npm
dependencies are consumed as published packages under their own licences; see each
project's `PackageReference` set and `Directory.Packages.props`.

## Bundled front-end assets

The default site theme ships third-party static assets in
`Crest.Site/wwwroot/`:

| Asset | Licence |
| --- | --- |
| The theme's HTML/CSS, from a Start Bootstrap template | MIT, Copyright (c) 2013-2020 Start Bootstrap LLC — `Crest.Site/wwwroot/LICENSE` |
| `vendor/bootstrap` — Bootstrap v4.5.0 | MIT, Copyright 2011-2020 The Bootstrap Authors and Twitter, Inc. |
| `vendor/jquery` — jQuery v3.5.1 | MIT, Copyright OpenJS Foundation and other contributors |
| `vendor/fontawesome-free` — Font Awesome Free 5.13.0 | Icons CC BY 4.0, fonts SIL OFL 1.1, code MIT — Copyright Fonticons, Inc.; see https://fontawesome.com/license/free |

Each file retains the licence header its publisher ships it with.

## Reference data

`Crest.Money/Data/currencies.json` is ISO 4217 reference data (codes, names, symbols and
minor units). `Crest.Regions` and `Crest.Global` ship geographic and list reference data on
the same footing: facts, not third-party works.
