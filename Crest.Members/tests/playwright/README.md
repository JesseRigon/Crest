# Crest.Members live checks

Registered in `dev/run-admin-suite.js`; run one with `CHECK_FILTER=<name> node dev/run-admin-suite.js`.

checks/members-memberships-api.js — memberships backend round trip over subscriptions
and seats: a Perk option on `members.perk`, a tier carrying the permission CEILING, an
org-scoped group carrying the GRANT, a subscription an org buys, and seats assigned to
portal users under it. Asserts the rules that make entitlement resolution total — the
(member, organization) key unique across EVERY subscription in that org, a paid
subscription refusing when full, an inactive seat releasing its slot, the free
subscription never running out and only one per org, a group from another org being
refused, and resolution intersecting the group's grant with the tier's ceiling. Seats
and subscriptions are deleted afterwards, member org bindings removed, test perks
hidden (options never delete).
