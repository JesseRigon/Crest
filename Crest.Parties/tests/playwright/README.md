# Crest.Parties live checks

Registered in the shared suite (`../../../tests/playwright/run-admin-suite.js`) and in a
consuming host's own entry script; run one with `CHECK_FILTER=<name>`.

```
checks/party-contacts-api.js    contact points and addresses on a party
checks/party-positions-api.js   a person's positions in an organization, and the
                                organization's people
```

Parties registers no party role of its own, so the generic role pages (All Parties, the
per-role list) are exercised by whichever module registers a role — in a consuming host,
its own checks.

The server defaults to `http://crest.localhost:5010`. Override it with `BASE_URL`;
credentials come from `ADMIN_USER`/`ADMIN_USERNAME` and `ADMIN_PASSWORD`, and browser
visibility can be enabled with `HEADED=1`.
