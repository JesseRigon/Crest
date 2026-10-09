const { createInstance } = require('../../../../../Crest/tests/playwright/harness/instance');
const { loginAsUser } = require('../../../../../Crest/tests/playwright/harness/auth');
const { ensureTestUser } = require('../../../../../Crest/tests/playwright/harness/testUsers');
const { fetchAntiforgeryToken } = require('../../../../../Crest/tests/playwright/harness/antiforgery');

// Phase 0a of docs/workflows.md: the Crest.Workflows engine runs inside the tenant and its API is
// gated by Orchard. As admin: list definitions; create + publish a one-activity flow;
// execute it; read the journal. A content-published trigger on Item, with an acting user,
// runs once the item is published and correlates to the item. Security: writes without
// the Crest antiforgery header are 400; a limited role gets 403; anonymous gets 401.
// Cleans up what it creates.
module.exports = async function run(page, ctx) {
  const results = [];
  const api = `${ctx.baseUrl}/crest-workflows/api`;
  const created = { definitions: [], items: [] };

  async function call(method, url, body, { antiforgery = true } = {}) {
    const token = antiforgery ? await fetchAntiforgeryToken(page, ctx.baseUrl) : null;
    return page.evaluate(async ({ method, url, body, token }) => {
      // No JSON content type on body-less requests: FastEndpoints would try to read a body.
      const headers = body === undefined ? {} : { 'Content-Type': 'application/json' };
      if (token) headers[token.headerName || 'RequestVerificationToken'] = token.requestToken;
      try {
        const response = await fetch(url, { method, credentials: 'include', headers, body: body === undefined ? undefined : JSON.stringify(body) });
        const text = await response.text();
        let json = null;
        try { json = text ? JSON.parse(text) : null; } catch {}
        return { ok: response.ok, status: response.status, text, json };
      } catch (error) {
        return { ok: false, status: 0, text: `${method} ${url}: ${error.message}`, json: null };
      }
    }, { method, url, body, token });
  }

  function literal(value, typeName = 'String') {
    return { typeName, expression: { type: 'Literal', value } };
  }

  async function saveDefinition(name, root) {
    // Crest.Workflows 3.6's save endpoint dereferences the collections without null checks, so an
    // API client must send them empty rather than omit them.
    const response = await call('POST', `${api}/workflow-definitions`, {
      model: { name, root, variables: [], inputs: [], outputs: [], outcomes: [], customProperties: {} },
      publish: true,
    });
    const id = response.json?.definitionId ?? response.json?.workflowDefinition?.definitionId;
    if (id) created.definitions.push(id);
    return { response, id };
  }

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

  try {
    // 1. The API answers under the tenant, for the admin.
    const list = await call('GET', `${api}/workflow-definitions`);
    results.push({ name: 'lists-definitions', pass: list.ok && Array.isArray(list.json?.items), message: `HTTP ${list.status} ${list.text.slice(0, 120)}` });

    // 2. Create + publish a one-activity flow and execute it.
    const stamp = Date.now();
    const hello = await saveDefinition(`Playwright hello ${stamp}`, { type: 'Crest.Workflows.WriteLine', id: 'hello', version: 1, text: literal(`hello from workflow ${stamp}`) });
    results.push({ name: 'creates-and-publishes-definition', pass: hello.response.ok && Boolean(hello.id), message: `HTTP ${hello.response.status} ${hello.response.text.slice(0, 200)}` });

    let executed = { ok: false, status: 0, text: '', json: null };
    if (hello.id) {
      executed = await call('POST', `${api}/workflow-definitions/${encodeURIComponent(hello.id)}/execute`, {});
    }
    const status = executed.json?.workflowState?.status;
    const instanceId = executed.json?.workflowState?.id;
    results.push({ name: 'executes-to-finished', pass: executed.ok && status === 'Finished', message: `HTTP ${executed.status} status=${status} ${executed.text.slice(0, 160)}` });

    if (instanceId) {
      const journal = await waitFor(async () => {
        const response = await call('GET', `${api}/workflow-instances/${encodeURIComponent(instanceId)}/journal`);
        return response.ok && (response.json?.items?.length ?? 0) > 0 ? response : null;
      });
      const entries = journal?.json?.items ?? [];
      results.push({ name: 'journal-records-the-activity', pass: entries.some(entry => entry.activityType === 'Crest.Workflows.WriteLine'), message: `entries=${entries.length} types=${[...new Set(entries.map(entry => entry.activityType))].join(',')}` });
    } else {
      results.push({ name: 'journal-records-the-activity', pass: false, message: 'no instance id from execute' });
    }

    // 3. Content triggers, fired by publishing an Item. Crest.Workflows indexes a trigger only when its
    //    node is marked canStartWorkflow; the diagnostics endpoint shows what got indexed.
    //    Three flows: unguarded; guarded by a permission the admin holds (runs); guarded by
    //    a permission nobody holds (ends on Denied without running the task).
    const triggerFlow = (requiredPermission) => ({
      type: 'Crest.Workflows.Flowchart', id: 'flow', version: 1,
      activities: [
        {
          type: 'OrchardCore.Content.ContentPublished', id: 'published', version: 1,
          contentTypes: ['Item'],
          ...(requiredPermission ? { requiredPermission: literal(requiredPermission) } : {}),
          customProperties: { canStartWorkflow: true },
        },
        { type: 'Crest.Workflows.WriteLine', id: 'log', version: 1, text: literal('item published') },
      ],
      connections: [{ source: { activity: 'published', port: 'Done' }, target: { activity: 'log', port: 'In' } }],
    });
    const flows = {
      open: await saveDefinition(`Playwright content trigger ${stamp}`, triggerFlow(null)),
      allowed: await saveDefinition(`Playwright guarded trigger (allowed) ${stamp}`, triggerFlow('ManageWorkflows')),
      denied: await saveDefinition(`Playwright guarded trigger (denied) ${stamp}`, triggerFlow('NoSuchPermission')),
    };
    results.push({ name: 'publishes-content-trigger-definitions', pass: Object.values(flows).every(flow => flow.response.ok && flow.id), message: Object.entries(flows).map(([key, flow]) => `${key}=${flow.response.status}`).join(' ') });

    const indexed = await call('GET', `${ctx.baseUrl}/api/crest/workflows/triggers`);
    const indexedIds = new Set((indexed.json || []).map(trigger => trigger.definitionId));
    results.push({ name: 'publishing-indexes-the-content-triggers', pass: indexed.ok && Object.values(flows).every(flow => indexedIds.has(flow.id)), message: `HTTP ${indexed.status} indexed=${indexedIds.size}` });

    const item = await call('POST', `${ctx.baseUrl}/api/crest/content-items`, {
      contentType: 'Item', displayText: `Playwright workflow item ${stamp}`, publish: true,
      content: { AssetPart: { Number: { Text: `PWW-${stamp}` } } },
    });
    const itemId = item.json?.contentItemId;
    if (itemId) created.items.push(itemId);

    async function instancesOf(definitionId) {
      return waitFor(async () => {
        const response = await call('GET', `${api}/workflow-instances?definitionId=${encodeURIComponent(definitionId)}&correlationId=${encodeURIComponent(itemId)}`);
        return response.ok && (response.json?.items?.length ?? 0) > 0 ? response.json.items : null;
      });
    }
    async function ranTheTask(instance) {
      if (!instance) return false;
      const journal = await call('GET', `${api}/workflow-instances/${encodeURIComponent(instance.id)}/journal`);
      return (journal.json?.items ?? []).some(entry => entry.activityType === 'Crest.Workflows.WriteLine' && entry.eventName === 'Completed');
    }

    const open = itemId && flows.open.id ? await instancesOf(flows.open.id) : null;
    results.push({
      name: 'content-published-starts-the-flow-for-that-item',
      pass: item.ok && open?.length === 1 && open[0].correlationId === itemId && open[0].status === 'Finished' && await ranTheTask(open[0]),
      message: `item=${item.status} instances=${open?.length ?? 0} status=${open?.[0]?.status ?? '-'}`,
    });

    const allowed = itemId && flows.allowed.id ? await instancesOf(flows.allowed.id) : null;
    results.push({
      name: 'trigger-guarded-by-a-held-permission-runs',
      pass: allowed?.length === 1 && await ranTheTask(allowed[0]),
      message: `instances=${allowed?.length ?? 0} status=${allowed?.[0]?.status ?? '-'}`,
    });

    const denied = itemId && flows.denied.id ? await instancesOf(flows.denied.id) : null;
    results.push({
      name: 'trigger-guarded-by-a-missing-permission-ends-on-denied',
      pass: denied?.length === 1 && denied[0].status === 'Finished' && !(await ranTheTask(denied[0])),
      message: `instances=${denied?.length ?? 0} status=${denied?.[0]?.status ?? '-'} ranTask=${denied?.length ? await ranTheTask(denied[0]) : '-'}`,
    });

    // 4. Security: writes need the antiforgery header even for the admin.
    const noToken = await call('POST', `${api}/workflow-definitions`, { model: { name: 'nope' }, publish: false }, { antiforgery: false });
    results.push({ name: 'write-without-antiforgery-is-400', pass: noToken.status === 400, message: `HTTP ${noToken.status}` });

    // 5. Security: a limited role is refused (403), anonymous is refused (401). The stock
    //    OrchardCore.Workflows stereotypes grant ManageWorkflows to Editor as well as
    //    Administrator (feature enable applies them), so the limited role must be one
    //    outside that pair - Contributor or Author on a stock tenant.
    const roles = await call('GET', `${ctx.baseUrl}/api/crest/roles`);
    const limitedRole = ['Contributor', 'Author', 'Moderator']
      .map(name => (roles.json || []).find(role => role.name === name && !role.isAdmin))
      .find(Boolean) ?? (roles.json || []).find(role => !role.isAdmin && !role.isSystem && role.name !== 'Editor');
    if (limitedRole) {
      const user = await ensureTestUser(page, ctx.baseUrl, '3', { roles: [limitedRole.name] });
      const session = await createInstance();
      try {
        await loginAsUser(session.page, ctx.baseUrl, user);
        const denied = await session.page.evaluate(async (url) => {
          try { const response = await fetch(url, { credentials: 'include' }); return response.status; } catch (error) { return `fetch failed: ${error.message}`; }
        }, `${api}/workflow-definitions`);
        results.push({ name: 'limited-role-gets-403', pass: denied === 403, message: `role=${limitedRole.name} HTTP ${denied}` });
      } finally {
        await session.browser.close().catch(() => {});
      }
    } else {
      results.push({ name: 'limited-role-gets-403', pass: false, message: 'no non-admin role on this tenant' });
    }

    const anonymous = await createInstance();
    try {
      // A fresh context on about:blank has no origin to fetch from; land on the login page first.
      await anonymous.page.goto(`${ctx.baseUrl}/login`, { waitUntil: 'domcontentloaded' });
      const anonymousStatus = await anonymous.page.evaluate(async (url) => {
        try { const response = await fetch(url, { credentials: 'include', redirect: 'manual' }); return response.status; } catch (error) { return `fetch failed: ${error.message}`; }
      }, `${api}/workflow-definitions`);
      results.push({ name: 'anonymous-gets-401', pass: anonymousStatus === 401, message: `HTTP ${anonymousStatus}` });
    } finally {
      await anonymous.browser.close().catch(() => {});
    }
  } finally {
    for (const id of created.items) {
      await call('DELETE', `${ctx.baseUrl}/api/crest/content-items/${encodeURIComponent(id)}`).catch(() => {});
    }
    for (const id of created.definitions) {
      await call('DELETE', `${api}/workflow-definitions/${encodeURIComponent(id)}`).catch(() => {});
    }
  }

  return results;
};
