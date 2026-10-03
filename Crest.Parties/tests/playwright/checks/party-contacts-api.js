// Live round trip of the party contact data API (api/crest/parties/{id}/...): the
// ContactPoints/Addresses bags are written through this surface (the generic
// content-items editor round-trips but cannot edit bag contents), so this is the
// check that the whole path works on a provisioned tenant - kind keys resolve to the
// seeded option lists, the per-address geo stack is validated against the tree server-side,
// "preferred" is exclusive per kind, and a generic field edit of the party leaves the
// bags intact. Creates and always cleans up its own Person.
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

  const displayText = `Playwright person ${Date.now()}`;
  const created = await api('POST', '/api/crest/content-items', { contentType: 'Person', displayText, publish: true });
  const personId = created.json?.contentItemId;
  check('create-person', created.ok && !!personId, `HTTP ${created.status}`);
  if (!personId) return results;

  const base = `/api/crest/parties/${encodeURIComponent(personId)}`;

  try {
    // Contact points: an email, then a preferred mobile, then a landline.
    const email = await api('POST', `${base}/contact-points`, { kind: 'email', value: 'probe@example.com', label: 'Work' });
    check('add-email', email.ok && email.json?.kind === 'email' && email.json?.value === 'probe@example.com', `HTTP ${email.status} ${email.text}`);

    const mobile = await api('POST', `${base}/contact-points`, { kind: 'mobile', value: '+1 208 555 0100', preferred: true, phoneCountry: 'US' });
    check('add-preferred-mobile', mobile.ok && mobile.json?.preferred === true && mobile.json?.phoneCountry === 'US', `HTTP ${mobile.status} ${mobile.text}`);

    const landline = await api('POST', `${base}/contact-points`, { kind: 'phone', value: '+1 208 555 0199' });
    check('add-landline', landline.ok && landline.json?.kind === 'phone', `HTTP ${landline.status} ${landline.text}`);

    const badKind = await api('POST', `${base}/contact-points`, { kind: 'carrier-pigeon', value: 'coo' });
    check('unknown-kind-is-400', badKind.status === 400, `HTTP ${badKind.status} ${badKind.text}`);

    // Addresses: the geo stack is validated on the API, not just in the editor - a level-2
    // node must sit beneath the level-1 country (plans/regions-and-locations.md).
    const mismatch = await api('POST', `${base}/addresses`, { kind: 'billing', country: 'CA', levelNodeIds: { 2: 'US-ID' }, locality: 'Boise', postalCode: 'K1A 0B1' });
    check('region-must-belong-to-country', mismatch.status === 400, `HTTP ${mismatch.status} ${mismatch.text}`);

    // The country's addressing map validates the text: a US ZIP must match its pattern.
    const badZip = await api('POST', `${base}/addresses`, { kind: 'billing', line1: '1 Main St', locality: 'Boise', country: 'US', levelNodeIds: { 2: 'US-ID' }, postalCode: 'ABCDE' });
    check('postal-code-must-match-map', badZip.status === 400, `HTTP ${badZip.status} ${badZip.text}`);

    const billing = await api('POST', `${base}/addresses`, { kind: 'billing', line1: '1 Main St', locality: 'Boise', country: 'US', levelNodeIds: { 2: 'US-ID' }, postalCode: '83702', preferred: true });
    check('add-billing-address', billing.ok && billing.json?.country === 'US' && billing.json?.levelNodeIds?.['2'] === 'US-ID' && billing.json?.levelNodeIds?.['1'] === 'US' && billing.json?.kind === 'billing', `HTTP ${billing.status} ${billing.text}`);

    // Preferred is exclusive per kind: a second preferred billing address demotes the first.
    const billing2 = await api('POST', `${base}/addresses`, { kind: 'billing', line1: '2 Side St', locality: 'Denver', country: 'US', levelNodeIds: { 2: 'US-CO' }, postalCode: '80202', preferred: true });
    check('add-second-preferred-billing', billing2.ok && billing2.json?.preferred === true, `HTTP ${billing2.status} ${billing2.text}`);

    let contacts = await api('GET', `${base}/contacts`, null);
    const preferredBilling = (contacts.json?.addresses || []).filter((a) => a.kind === 'billing' && a.preferred);
    check('read-contacts', contacts.ok && contacts.json?.contactPoints?.length === 3 && contacts.json?.addresses?.length === 2, `HTTP ${contacts.status} ${contacts.text}`);
    check('preferred-is-exclusive-per-kind', preferredBilling.length === 1 && preferredBilling[0]?.id === billing2.json?.id, `preferred billing ids: ${preferredBilling.map((a) => a.id).join(',')}`);

    // Update: swap the preferred phone family entry to the landline.
    const promoted = await api('PUT', `${base}/contact-points/${encodeURIComponent(landline.json?.id)}`, { kind: 'phone', value: '+1 208 555 0199', preferred: true });
    check('update-contact-point', promoted.ok && promoted.json?.preferred === true, `HTTP ${promoted.status} ${promoted.text}`);
    contacts = await api('GET', `${base}/contacts`, null);
    const mobileNow = (contacts.json?.contactPoints || []).find((p) => p.id === mobile.json?.id);
    // Different KINDS (mobile vs phone), so promoting the landline must NOT demote the mobile.
    check('preferred-scoped-to-kind', mobileNow?.preferred === true, `mobile preferred=${mobileNow?.preferred}`);

    // A generic field edit of the party (what the content-items editor does) must
    // round-trip the bags untouched.
    const current = await page.evaluate(async (id) => {
      const response = await fetch(`/api/crest/content-items/${encodeURIComponent(id)}`, { credentials: 'include' });
      return response.json();
    }, personId);
    const content = current?.content || {};
    content.Person = { ...(content.Person || {}), FirstName: { Text: 'Probe' } };
    const edited = await api('PUT', `/api/crest/content-items/${encodeURIComponent(personId)}`, { contentType: 'Person', displayText, content, publish: true });
    contacts = await api('GET', `${base}/contacts`, null);
    check('generic-edit-keeps-bags', edited.ok && contacts.json?.contactPoints?.length === 3 && contacts.json?.addresses?.length === 2, `edit HTTP ${edited.status}; points=${contacts.json?.contactPoints?.length} addresses=${contacts.json?.addresses?.length}`);

    // Remove one of each.
    const removedPoint = await api('DELETE', `${base}/contact-points/${encodeURIComponent(email.json?.id)}`, null);
    const removedAddress = await api('DELETE', `${base}/addresses/${encodeURIComponent(billing.json?.id)}`, null);
    const missing = await api('DELETE', `${base}/addresses/does-not-exist`, null);
    contacts = await api('GET', `${base}/contacts`, null);
    check('remove-entries', removedPoint.status === 204 && removedAddress.status === 204 && missing.status === 404 && contacts.json?.contactPoints?.length === 2 && contacts.json?.addresses?.length === 1,
      `point ${removedPoint.status}, address ${removedAddress.status}, missing ${missing.status}, left points=${contacts.json?.contactPoints?.length} addresses=${contacts.json?.addresses?.length}`);

    // Party views derive Email/Phone from the bags (customer list shape).
    const notParty = await api('GET', '/api/crest/parties/not-a-party/contacts', null);
    check('unknown-party-is-404', notParty.status === 404, `HTTP ${notParty.status}`);
  } finally {
    await api('DELETE', `/api/crest/content-items/${encodeURIComponent(personId)}`, null).catch(() => {});
  }

  return results;
};
