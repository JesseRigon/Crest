// Live round trip of org structure (api/crest/parties/{id}/positions and
// /{orgId}/people): positions are bag-contained on the Person, and the
// organization-side "who works here" answer comes from PartyPositionIndex - so this
// is the check that the index maps bag contents on save. Creates and always cleans
// up its own Organization and Person.
module.exports = async function run(page, ctx) {
  async function getAntiforgeryToken() {
    return page.evaluate(async () => {
      const response = await fetch('/api/crest/antiforgery/token', { credentials: 'include' });
      if (!response.ok) throw new Error(`antiforgery failed: ${response.status}`);
      return response.json();
    });
  }

  async function api(method, url, body) {
    const token = await getAntiforgeryToken();
    return page.evaluate(
      async ({ method, url, body, token }) => {
        const response = await fetch(url, {
          method,
          credentials: 'include',
          headers: {
            'Content-Type': 'application/json',
            [token.headerName || 'RequestVerificationToken']: token.requestToken,
          },
          body: body === null || body === undefined ? undefined : JSON.stringify(body),
        });
        const text = await response.text();
        let json = null;
        try {
          json = text ? JSON.parse(text) : null;
        } catch {}
        return { ok: response.ok, status: response.status, text, json };
      },
      { method, url, body, token },
    );
  }

  const results = [];
  const check = (name, pass, message) => results.push({ name, pass, message });
  const stamp = Date.now();

  const org = await api('POST', '/api/crest/content-items', { contentType: 'Organization', displayText: `Playwright org ${stamp}`, publish: true });
  const person = await api('POST', '/api/crest/content-items', { contentType: 'Person', displayText: `Playwright person ${stamp}`, publish: true });
  const orgId = org.json?.contentItemId;
  const personId = person.json?.contentItemId;
  check('create-org-and-person', org.ok && person.ok && !!orgId && !!personId, `org HTTP ${org.status}, person HTTP ${person.status}`);
  if (!orgId || !personId) return results;

  const personBase = `/api/crest/parties/${encodeURIComponent(personId)}`;
  const orgBase = `/api/crest/parties/${encodeURIComponent(orgId)}`;

  try {
    const notAnOrg = await api('POST', `${personBase}/positions`, { organizationId: personId, title: 'Nope' });
    check('organization-must-be-an-organization', notAnOrg.status === 400, `HTTP ${notAnOrg.status} ${notAnOrg.text}`);

    const first = await api('POST', `${personBase}/positions`, { organizationId: orgId, title: 'Engineer', department: 'R&D', primary: true });
    check('add-primary-position', first.ok && first.json?.organizationId === orgId && first.json?.organizationName?.includes('Playwright org') && first.json?.primary === true, `HTTP ${first.status} ${first.text}`);

    const second = await api('POST', `${personBase}/positions`, { organizationId: orgId, title: 'Board member', primary: true });
    check('add-second-primary-position', second.ok && second.json?.primary === true, `HTTP ${second.status} ${second.text}`);

    let positions = await api('GET', `${personBase}/positions`, null);
    const primaries = (positions.json || []).filter((p) => p.primary);
    check('primary-is-exclusive-per-person', positions.ok && positions.json?.length === 2 && primaries.length === 1 && primaries[0]?.id === second.json?.id, `HTTP ${positions.status} ${positions.text}`);

    // Organization side, through the index.
    let people = await api('GET', `${orgBase}/people`, null);
    const mine = (people.json || []).filter((entry) => entry.personId === personId);
    check('people-in-organization-via-index', people.ok && mine.length === 2 && mine.some((entry) => entry.title === 'Engineer'), `HTTP ${people.status} ${people.text}`);

    const peopleOfPerson = await api('GET', `${personBase}/people`, null);
    check('people-lookup-is-organization-only', peopleOfPerson.status === 404, `HTTP ${peopleOfPerson.status}`);

    const updated = await api('PUT', `${personBase}/positions/${encodeURIComponent(first.json?.id)}`, { organizationId: orgId, title: 'Principal engineer', department: 'R&D', primary: false });
    check('update-position', updated.ok && updated.json?.title === 'Principal engineer', `HTTP ${updated.status} ${updated.text}`);

    const removed = await api('DELETE', `${personBase}/positions/${encodeURIComponent(second.json?.id)}`, null);
    people = await api('GET', `${orgBase}/people`, null);
    const left = (people.json || []).filter((entry) => entry.personId === personId);
    check('remove-position-updates-index', removed.status === 204 && left.length === 1 && left[0]?.title === 'Principal engineer', `HTTP ${removed.status}; left=${JSON.stringify(left)}`);
  } finally {
    await api('DELETE', `/api/crest/content-items/${encodeURIComponent(personId)}`, null).catch(() => {});
    await api('DELETE', `/api/crest/content-items/${encodeURIComponent(orgId)}`, null).catch(() => {});
  }

  return results;
};
