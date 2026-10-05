// Definition locks (docs/content-items.md › Definition locks) from the tenant side, on a
// part this check owns (CrestLockCheckPart, created if absent and left in place - there is
// no Crest endpoint that deletes a part, and the check is idempotent). A Tenant lock on a
// field refuses retyping, a visibility condition and an option-picker conversion (409, with
// the reason), a Module lock cannot be placed from the tenant, lifting the lock lets the
// retype through, and a part lock covers every field while still allowing new ones.
module.exports = async function run(page, ctx) {
  const results = [];
  const part = 'CrestLockCheckPart';
  const field = 'Code';
  const types = `${ctx.baseUrl}/api/crest/content-types`;

  async function token() {
    return page.evaluate(async () => {
      const response = await fetch('/api/crest/antiforgery/token', { credentials: 'include' });
      if (!response.ok) throw new Error(`antiforgery failed: ${response.status}`);
      return response.json();
    });
  }

  async function api(method, url, body) {
    const t = await token();
    return page.evaluate(async ({ method, url, body, t }) => {
      const headers = body === undefined ? {} : { 'Content-Type': 'application/json' };
      headers[t.headerName || 'RequestVerificationToken'] = t.requestToken;
      const response = await fetch(url, { method, credentials: 'include', headers, body: body === undefined ? undefined : JSON.stringify(body) });
      const text = await response.text();
      let json = null;
      try { json = text ? JSON.parse(text) : null; } catch {}
      return { ok: response.ok, status: response.status, text, json };
    }, { method, url, body, t });
  }

  async function partModel() {
    const parts = await api('GET', `${types}/parts`);
    return (parts.json || []).find(p => p.name === part) || null;
  }
  const fieldLock = (model, name) => (model?.part?.fields || []).find(f => f.name === name)?.lock;

  // A field of the check's own; also the part's existence. Idempotent.
  const created = await api('PUT', `${types}/fields`, { part, field, fieldType: 'TextField', displayName: 'Code' });
  results.push({ name: 'check-part-and-field-exist', pass: created.status === 204 && Boolean(await partModel()), message: `HTTP ${created.status}` });

  // Start clean whatever an earlier run left.
  await api('PUT', `${types}/locks`, { part, lock: 'None' });
  await api('PUT', `${types}/locks`, { part, field, lock: 'None' });

  const locked = await api('PUT', `${types}/locks`, { part, field, lock: 'Tenant' });
  const afterLock = await partModel();
  results.push({ name: 'tenant-lock-is-placed-and-reported', pass: locked.status === 204 && fieldLock(afterLock, field) === 'Tenant' && afterLock?.part?.lock === 'None', message: `HTTP ${locked.status} field=${fieldLock(afterLock, field)} part=${afterLock?.part?.lock}` });

  const retype = await api('PUT', `${types}/fields`, { part, field, fieldType: 'NumericField' });
  const hide = await api('PUT', `${ctx.baseUrl}/api/crest/field-visibility`, { part, field, settings: { path: 'Other', operator: 'Any', keys: [] } });
  const picker = await api('PUT', `${ctx.baseUrl}/api/crest/option-picker/attachment`, { part, field, settings: { sourceKey: 'contentitem:ContentPartList' } });
  const relabel = await api('PUT', `${types}/fields`, { part, field, fieldType: 'TextField', displayName: 'Code (relabelled)' });
  results.push({ name: 'locked-field-refuses-retype-hide-and-picker-with-the-reason', pass: retype.status === 409 && /locked/.test(retype.json?.detail || retype.text) && hide.status === 409 && picker.status === 409, message: `retype=${retype.status} hide=${hide.status} picker=${picker.status} ${(retype.json?.detail || retype.text).slice(0, 120)}` });
  // Same type, new display name: the display surface is not the lock's business (a no-op here, since the type is unchanged).
  results.push({ name: 'display-surface-stays-editable', pass: relabel.status === 204, message: `HTTP ${relabel.status}` });

  const moduleLock = await api('PUT', `${types}/locks`, { part, field, lock: 'Module' });
  results.push({ name: 'a-module-lock-cannot-be-placed-from-the-tenant', pass: moduleLock.status === 409, message: `HTTP ${moduleLock.status} ${(moduleLock.json?.detail || moduleLock.text).slice(0, 120)}` });

  const lifted = await api('PUT', `${types}/locks`, { part, field, lock: 'None' });
  const retypeAfter = await api('PUT', `${types}/fields`, { part, field, fieldType: 'NumericField' });
  const backToText = await api('PUT', `${types}/fields`, { part, field, fieldType: 'TextField' });
  results.push({ name: 'lifting-the-lock-lets-the-retype-through', pass: lifted.status === 204 && retypeAfter.status === 204 && backToText.status === 204, message: `lift=${lifted.status} retype=${retypeAfter.status} back=${backToText.status}` });

  const partLocked = await api('PUT', `${types}/locks`, { part, lock: 'Tenant' });
  const afterPartLock = await partModel();
  const retypeUnderPartLock = await api('PUT', `${types}/fields`, { part, field, fieldType: 'NumericField' });
  const addUnderPartLock = await api('PUT', `${types}/fields`, { part, field: 'Extra', fieldType: 'TextField' });
  const afterAdd = await partModel();
  results.push({ name: 'a-part-lock-covers-every-field-and-allows-new-ones', pass: partLocked.status === 204 && afterPartLock?.part?.lock === 'Tenant' && fieldLock(afterPartLock, field) === 'Tenant' && retypeUnderPartLock.status === 409 && addUnderPartLock.status === 204 && fieldLock(afterAdd, 'Extra') === 'Tenant', message: `lock=${partLocked.status} part=${afterPartLock?.part?.lock} field=${fieldLock(afterPartLock, field)} retype=${retypeUnderPartLock.status} add=${addUnderPartLock.status} extra=${fieldLock(afterAdd, 'Extra')}` });

  const partLifted = await api('PUT', `${types}/locks`, { part, lock: 'None' });
  const bogus = await api('PUT', `${types}/locks`, { part: 'NoSuchPartHere', lock: 'Tenant' });
  results.push({ name: 'part-lock-lifts-and-unknown-parts-are-404', pass: partLifted.status === 204 && bogus.status === 404, message: `lift=${partLifted.status} unknown=${bogus.status}` });

  return results;
};
