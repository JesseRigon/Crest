# Accounting Playwright probes

Accounting-specific checks live under `checks/` and are run as part of
`dev/run-admin-suite.js` (see repo root `dev/dev.sh test`), not invoked individually:

```
checks/feature-enable.js
checks/party-contacts-api.js
checks/party-positions-api.js
checks/customers-route-access.js
checks/customer-filter-refresh.js
```

The app defaults to `http://crest.localhost:5010`. Override it with `BASE_URL`; credentials
use `ADMIN_USER` and `ADMIN_PASSWORD`, and browser visibility can be enabled with `HEADED=1`.
