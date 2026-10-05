# Third-party notices

The Crest admin application is built on `Crest.Components`, which is
substantially derived from Radzen Blazor (MIT, Copyright (c) 2018-2026 Radzen Ltd), and
also uses Radzen Blazor as a published package. The full attribution and licence text are
in `../Crest.Components/NOTICE.md`; the repository-wide inventory is in
`../NOTICE.md`.

Radzen's sidebar is deliberately not used: `CrestPrimaryNavMenu`, supplied by
`Crest.Components`, is a duplicated implementation of Radzen's `RadzenSidebar`
that neither references nor inherits from it.
