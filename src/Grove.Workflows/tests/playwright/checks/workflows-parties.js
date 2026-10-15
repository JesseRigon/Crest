// Parties registers with the workflow registry: role created/removed triggers appear, and a
// role composed onto a party (Grove's fixture role, SampleRole - workflows-fixtures-enable
// turns it on) raises party.role-created with the role id
// as correlation and the party id in the payload; removing it raises party.role-removed.
// Base parties (Person, Organization) raise nothing. Cleans up what it creates.
module.exports = async function run(page, ctx) {
  const results = [];
  const engine = `${ctx.baseUrl}/grove-workflows/api`;
  const created = { definitions: [], items: [] };

  async function token() {
    return page.evaluate(async () => {
      const response = await fetch('/api/grove/antiforgery/token', { credentials: 'include' });
      if (!response.ok) throw new Error(`antiforgery failed: ${response.status}`);
      return response.json();
    });
  }
  async function call(method, url, body) {
    const t = await token();
    return page.evaluate(async ({ method, url, body, t }) => {
      const headers = body === undefined ? {} : { 'Content-Type': 'application/json' };
      headers[t.headerName || 'RequestVerificationToken'] = t.requestToken;
      try {
        const response = await fetch(url, { method, credentials: 'include', headers, body: body === undefined ? undefined : JSON.stringify(body) });
        const text = await response.text();
        let json = null;
        try { json = text ? JSON.parse(text) : null; } catch {}
        return { ok: response.ok, status: response.status, text, json };
      } catch (error) {
        return { ok: false, status: 0, text: `${method} ${url}: ${error.message}`, json: null };
      }
    }, { method, url, body, t });
  }
  const literal = (value, typeName = 'String') => ({ typeName, expression: { type: 'Literal', value } });
  async function waitFor(predicate, { timeout = 15000, interval = 500 } = {}) {
    const deadline = Date.now() + timeout;
    let last;
    while (Date.now() < deadline) {
      last = await predicate();
      if (last) return last;
      await page.waitForTimeout(interval);
    }
    return last;
  }
  async function flowFor(triggerKey, name) {
    const saved = await call('POST', `${engine}/workflow-definitions`, {
      model: {
        name, variables: [], inputs: [], outputs: [], outcomes: [], customProperties: {}, options: {},
        root: { type: 'Grove.Workflows.Flowchart', id: 'flow', version: 1, activities: [
          { type: 'Grove.Workflows.RegistryTrigger', id: 'trigger', version: 1, triggerKey: literal(triggerKey), customProperties: { canStartWorkflow: true } },
          { type: 'Grove.Workflows.WriteLine', id: 'log', version: 1, text: literal(triggerKey) } ],
          connections: [{ source: { activity: 'trigger', port: 'Done' }, target: { activity: 'log', port: 'In' } }] },
      }, publish: true,
    });
    const id = saved.json?.workflowDefinition?.definitionId;
    if (id) created.definitions.push(id);
    return { saved, id };
  }
  async function instancesFor(definitionId, correlationId) {
    return waitFor(async () => {
      const response = await call('GET', `${engine}/workflow-instances?definitionId=${encodeURIComponent(definitionId)}&correlationId=${encodeURIComponent(correlationId)}`);
      return (response.json?.items?.length ?? 0) > 0 ? response.json.items : null;
    });
  }

  try {
    const registry = await call('GET', `${ctx.baseUrl}/api/grove/workflows/registry`);
    const keys = (registry.json?.triggers || []).map(t => t.key);
    results.push({ name: 'registry-lists-party-triggers', pass: registry.ok && keys.includes('party.role-created') && keys.includes('party.role-removed'), message: keys.join(',') });

    const stamp = Date.now();
    const onCreated = await flowFor('party.role-created', `Playwright role created ${stamp}`);
    const onRemoved = await flowFor('party.role-removed', `Playwright role removed ${stamp}`);
    results.push({ name: 'role-trigger-flows-publish', pass: Boolean(onCreated.id && onRemoved.id), message: `created=${onCreated.saved.status} removed=${onRemoved.saved.status}` });

    // A base party raises nothing; a role composed onto it raises role-created.
    const person = await call('POST', `${ctx.baseUrl}/api/grove/content-items`, { contentType: 'Person', displayText: `Playwright role party ${stamp}`, publish: true });
    const personId = person.json?.contentItemId;
    if (personId) created.items.push(personId);
    const role = await call('POST', `${ctx.baseUrl}/api/grove/content-items`, { contentType: 'SampleRole', displayText: `Playwright sample role ${stamp}`, publish: true, content: { SampleRole: { Party: { ContentItemIds: [personId] } } } });
    const roleId = role.json?.contentItemId;

    const createdRun = roleId ? await instancesFor(onCreated.id, roleId) : null;
    results.push({ name: 'composing-a-role-raises-role-created', pass: person.ok && role.ok && createdRun?.length === 1 && createdRun[0].status === 'Finished', message: `person=${person.status} role=${role.status} instances=${createdRun?.length ?? 0}` });

    const forPerson = await call('GET', `${engine}/workflow-instances?definitionId=${encodeURIComponent(onCreated.id)}&correlationId=${encodeURIComponent(personId)}`);
    results.push({ name: 'a-base-party-raises-nothing', pass: forPerson.ok && forPerson.json?.totalCount === 0, message: `instancesForPerson=${forPerson.json?.totalCount}` });

    if (createdRun?.[0]) {
      const journal = await call('GET', `${engine}/workflow-instances/${encodeURIComponent(createdRun[0].id)}/journal`);
      const trigger = (journal.json?.items || []).find(e => e.activityType === 'Grove.Workflows.RegistryTrigger' && e.eventName === 'Completed');
      const ranTask = (journal.json?.items || []).some(e => e.activityType === 'Grove.Workflows.WriteLine' && e.eventName === 'Completed');
      results.push({ name: 'role-created-flow-ran-through-the-trigger', pass: Boolean(trigger) && ranTask, message: `trigger completed=${Boolean(trigger)} task ran=${ranTask}` });
    }

    const removed = roleId ? await call('DELETE', `${ctx.baseUrl}/api/grove/content-items/${encodeURIComponent(roleId)}`) : { ok: false, status: 0 };
    const removedRun = roleId ? await instancesFor(onRemoved.id, roleId) : null;
    results.push({ name: 'removing-the-role-raises-role-removed', pass: removed.ok && removedRun?.length === 1 && removedRun[0].status === 'Finished', message: `delete=${removed.status} instances=${removedRun?.length ?? 0}` });
  } finally {
    for (const id of created.items) await call('DELETE', `${ctx.baseUrl}/api/grove/content-items/${encodeURIComponent(id)}`).catch(() => {});
    for (const id of created.definitions) await call('DELETE', `${engine}/workflow-definitions/${encodeURIComponent(id)}`).catch(() => {});
  }

  return results;
};
