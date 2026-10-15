# Third-party notices

The Grove admin application is built on `Grove.Components`, which is
substantially derived from Radzen Blazor (MIT, Copyright (c) 2018-2026 Radzen Ltd), and
also uses Radzen Blazor as a published package. The full attribution and licence text are
in `../Grove.Components/NOTICE.md`; the repository-wide inventory is in
`../NOTICE.md`.

Radzen's sidebar is deliberately not used: `GrovePrimaryNavMenu`, supplied by
`Grove.Components`, is a duplicated implementation of Radzen's `RadzenSidebar`
that neither references nor inherits from it.
