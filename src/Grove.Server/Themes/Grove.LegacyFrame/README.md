# Grove Grove UI Framework Legacy Frame

`Grove.LegacyFrame` is a hidden Grove admin theme owned by `Grove.Server`. It is backend infrastructure used by Grove Grove UI Framework to render standard Grove admin pages inside Blazor iframes.

Requests that include `?legacy-frame=1` bypass the Blazor admin shell and are rendered with this stripped frame theme. The layout intentionally omits Grove's normal admin navigation and header chrome because the UI chrome is owned by the Grove Admin theme/shell.

This theme is part of the server overlay because it participates in Grove theme selection and backend rendering. The iframe component and styling that display it belong with the Admin theme composition root, using shared primitives from `Grove.Components` where needed.

Version format follows Grove Grove UI Framework's five-part compatibility scheme:

```text
{platform-major}.{platform-minor}.{platform-patch}.{grove-security}.{grove-bug}
```

Current compatibility version: `3.0.0.0.0`.
