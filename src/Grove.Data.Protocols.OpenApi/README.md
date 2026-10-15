# Grove.Data.Protocols.OpenApi

Describes the tenant's HTTP API as an OpenAPI document and serves the documentation UIs.

## What it describes

Every endpoint in the API registry: the route groups that modules declare through
`IApiContributor`, and the registry's defined entries (docs/MVC/api.md › The seam). The
endpoints explorer reads those minimal endpoints directly, tagged per contributor, so there is
nothing to annotate or register for the document. A module's new endpoint appears in the
document as soon as its contributor's feature is enabled.

Operations are ordered by path and verb, so the document does not change when an unrelated
feature is enabled or disabled. The API is bearer-only, so the document declares a single HTTP
bearer scheme.

## Features

| Feature | Serves |
|---|---|
| `Grove.Data.Protocols.OpenApi` | The OpenAPI document and the settings group (Settings › OpenApi) |
| `Grove.Data.Protocols.OpenApi.SwaggerUI` | Swagger UI at `~/swagger` |
| `Grove.Data.Protocols.OpenApi.ReDocUI` | ReDoc at `~/redoc` |
| `Grove.Data.Protocols.OpenApi.ScalarUI` | Scalar at `~/scalar/v1` |

The documentation UIs acquire the tenant's OpenID Connect token silently and attach it to their
requests; they never show a manual "Authorize" step.

## Adding an endpoint

Declare it in its module's API contributor (explicit binding on every parameter, Grove.Validation
rules, `ApiProblems` for errors; docs/MVC/api.md › Principles). No client generation step
follows: Grove's clients call the API through their own typed services.
