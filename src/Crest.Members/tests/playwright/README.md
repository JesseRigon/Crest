# Crest.Members live checks

Registered in the shared suite (`../../../tests/playwright/run-admin-suite.js`) and in a
consuming host's own entry script; run one with `CHECK_FILTER=<name>`.

checks/members-portal-api.js — the member portal's surface and its gate: registering and
signing in through the portal, the portal session, the active-organization binding, and
the login-channel rule in both directions (a member account refused at the tenant login,
a staff account refused at the portal).

The commercial side of a member relationship — subscriptions, seats, tiers, perks,
entitlement resolution — is not Members' and is checked by whichever module owns it.
Members declares `IMemberLifecycleHandler` for it and knows nothing more.
