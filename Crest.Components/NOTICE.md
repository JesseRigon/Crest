# Third-party notices

## Radzen Blazor

**This library is substantially derived from [Radzen Blazor](https://github.com/radzenhq/radzen-blazor).**
Its component set was forked from Radzen's source (the history records the final step as
"Merge remaining RadzenSource components into Crest", 2026-07-28) and renamed `Radzen*` →
`Crest*`, with the namespace rewritten to `Crest.Components.*`. Component families
including the chart, data grid, scheduler, Gantt, HTML editor, spreadsheet, tree, upload,
form and dropdown primitives, and the shared models, services, enums and JavaScript that
support them, originate there. Later work reorganized the file layout
(`Components/ComponentPathMigration.plan.md`), changed behaviour and added components that
have no Radzen counterpart, but the library as a whole is a derivative work and is
distributed under Radzen Blazor's licence terms as well as Crest's own.

Radzen Blazor is Copyright (c) 2018-2026 Radzen Ltd and licensed under the MIT License:

> Permission is hereby granted, free of charge, to any person obtaining a copy of this
> software and associated documentation files (the "Software"), to deal in the Software
> without restriction, including without limitation the rights to use, copy, modify,
> merge, publish, distribute, sublicense, and/or sell copies of the Software, and to
> permit persons to whom the Software is furnished to do so, subject to the following
> conditions: The above copyright notice and this permission notice shall be included in
> all copies or substantial portions of the Software. THE SOFTWARE IS PROVIDED "AS IS",
> WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
> WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
> IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR
> OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
> OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
> SOFTWARE.

Radzen Blazor is also consumed as a published package elsewhere in Crest, under the same
licence.

## Note on an earlier version of this file

An earlier version of this notice named only
`Components/Layout/CrestPrimaryNavMenu.razor` and `Components/Inputs/CrestDropDown.razor`
as Radzen-derived and described them as Crest-owned implementations that neither
reference nor inherit from the Radzen components they replace. That is true of those two
files in the narrow sense, but it understated the library: the fork is the library, not
two files of it. The statement above supersedes it.
