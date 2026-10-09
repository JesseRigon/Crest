# Crest Crest UI Framework Legacy Frame

`Crest.LegacyFrame` is a hidden Crest admin theme owned by `Crest.Server`. It is backend infrastructure used by Crest Crest UI Framework to render standard Crest admin pages inside Blazor iframes.

Requests that include `?legacy-frame=1` bypass the Blazor admin shell and are rendered with this stripped frame theme. The layout intentionally omits Crest's normal admin navigation and header chrome because the UI chrome is owned by the Crest Admin theme/shell.

This theme is part of the server overlay because it participates in Crest theme selection and backend rendering. The iframe component and styling that display it belong with the Admin theme composition root, using shared primitives from `Crest.Components` where needed.

Version format follows Crest Crest UI Framework's five-part compatibility scheme:

```text
{platform-major}.{platform-minor}.{platform-patch}.{crest-security}.{crest-bug}
```

Current compatibility version: `3.0.0.0.0`.
