# Speed — performance and scaling

Tracks where Crest's load concentrates and the plans for carrying it: what has to stay fast,
what may need its own hardware, and the decisions that bear on it. Each item links to the
doc that owns the design.

## Planned

- [ ] **The permission system can run on separate hardware** (2026-10-06, later). About half
  of all queries are permission work: building each request's principal for its side and
  organization, the access policies' verdicts, the hierarchy's "everyone under me", the
  file access index. The permission system therefore gets an API that can be deployed apart
  from the web servers — the same service in-process for a small host, a separate service on
  its own hardware for a large one — without callers changing. Bears on
  [members.md](members.md) (the per-session cache, the hierarchy), [media.md](media.md)
  (the access index) and [queries.md](queries.md) (caller scope inside queries).

## Already shaped with speed in mind

- **One cache per session** for the built principal and policy verdict, refreshed on an
  interval and by version token, not rebuilt per request ([members.md](members.md)).
- **The tenant switcher** reads the device's account list, never opening every tenant's
  shell ([members.md](members.md) › Tenants in the switcher).
- **Theme clients and module assemblies load per shell, on demand, and only for enabled
  modules**
  ([shells-and-themes.md](shells-and-themes.md)).
- **The hierarchy is a closure table** (a YesSql map index), so "everyone under me" is one
  indexed query ([members.md](members.md) › Relational hierarchy).
- **Global reference data** (NAICS, the geo tree, currencies) is stored once at the host, not
  copied into every tenant ([global-store.md](global-store.md)).

## Open

Performance questions still in [crest-implementation-issues.md](crest-implementation-issues.md):
the access index's size and
recalculation (F3), search filtering (F5), audit feed paging at volume (A4), background
worker throughput (W5), and streaming query results (Q4).
