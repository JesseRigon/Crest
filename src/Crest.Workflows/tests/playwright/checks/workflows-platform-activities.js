const { fetchAntiforgeryToken } = require('../../../../../Crest/tests/playwright/harness/antiforgery');

// Stock Crest workflow activities run on the Crest workflow engine (docs/workflows.md,
// the override route): the upstream Contents module raises ContentPublishedEvent through
// IWorkflowManager, the tenant's manager turns it into an PlatformEvent stimulus, the stock
// event's own content-type filter is honoured, and a stock CreateContentTask runs with its
// stock Liquid properties and creates real content. Cleans up what it creates.
module.exports = async function run(page, ctx) {
  const results = [];
  const api = `${ctx.baseUrl}/crest-workflows/api`;
  const created = { definitions: [], items: [] };

  async function call(method, url, body) {
    const token = await fetchAntiforgeryToken(page, ctx.baseUrl);
    return page.evaluate(async ({ method, url, body, token }) => {
      const headers = body === undefined ? {} : { 'Content-Type': 'application/json' };
      headers[token.headerName || 'RequestVerificationToken'] = token.requestToken;
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

  try {
    const stamp = Date.now();
    const marker = `Playwright stock task ${stamp}`;

    // Stock event (filtered to Item) -> stock task that creates an Organization with a
    // Liquid-evaluated title. A different content type than the trigger's, so the created
    // item does not re-trigger the flow.
    const flow = {
      type: 'Crest.Workflows.Flowchart', id: 'flow', version: 1,
      activities: [
        { type: 'Crest.Workflows.PlatformEvent', id: 'published', version: 1, eventName: literal('ContentPublishedEvent'), propertiesJson: literal(JSON.stringify({ ContentTypeFilter: ['Item'] })), customProperties: { canStartWorkflow: true } },
        { type: 'Crest.Workflows.PlatformTask', id: 'create', version: 1, activityName: literal('CreateContentTask'), propertiesJson: literal(JSON.stringify({ ContentType: 'Organization', Publish: true, ContentProperties: { Expression: JSON.stringify({ DisplayText: marker }) } })) },
      ],
      connections: [{ source: { activity: 'published', port: 'Done' }, target: { activity: 'create', port: 'In' } }],
    };
    const saved = await call('POST', `${api}/workflow-definitions`, { model: { name: `Playwright stock activities ${stamp}`, root: flow, variables: [], inputs: [], outputs: [], outcomes: [], customProperties: {}, options: {} }, publish: true });
    const definitionId = saved.json?.workflowDefinition?.definitionId;
    if (definitionId) created.definitions.push(definitionId);
    results.push({ name: 'stock-event-and-task-definition-publishes', pass: saved.ok && Boolean(definitionId), message: `HTTP ${saved.status} ${saved.text.slice(0, 160)}` });

    const triggers = await call('GET', `${ctx.baseUrl}/api/crest/workflows/triggers?definitionId=${encodeURIComponent(definitionId)}`);
    results.push({ name: 'stock-event-is-indexed-as-a-trigger', pass: triggers.ok && (triggers.json || []).some(t => t.name === 'Crest.Workflows.PlatformEvent'), message: `HTTP ${triggers.status} ${triggers.text.slice(0, 160)}` });

    // A stock event the filter excludes: publishing an Organization must not start it.
    const decoy = await call('POST', `${ctx.baseUrl}/api/crest/content-items`, { contentType: 'Organization', displayText: `Playwright decoy org ${stamp}`, publish: true });
    if (decoy.json?.contentItemId) created.items.push(decoy.json.contentItemId);

    // The real one: publishing an Item goes through the stock Contents handler.
    const item = await call('POST', `${ctx.baseUrl}/api/crest/content-items`, { contentType: 'Item', displayText: `Playwright stock event item ${stamp}`, publish: true, content: { AssetPart: { Number: { Text: `PWO-${stamp}` } } } });
    const itemId = item.json?.contentItemId;
    if (itemId) created.items.push(itemId);

    const instances = await waitFor(async () => {
      const response = await call('GET', `${api}/workflow-instances?definitionId=${encodeURIComponent(definitionId)}`);
      return response.ok && (response.json?.items?.length ?? 0) > 0 ? response.json.items : null;
    });
    const forItem = (instances || []).filter(i => i.correlationId === itemId);
    const forDecoy = (instances || []).filter(i => i.correlationId === decoy.json?.contentItemId);
    results.push({ name: 'stock-content-published-starts-the-flow-for-an-item', pass: item.ok && forItem.length === 1 && forItem[0].status === 'Finished', message: `item=${item.status} instances=${instances?.length ?? 0} forItem=${forItem.length} status=${forItem[0]?.status ?? '-'}` });

    // The stimulus is keyed by event name, so a non-matching content type still starts an
    // instance; the stock filter then ends it at the event (no task runs). That is the
    // stock engine's CanExecute contract, kept.
    async function ranTask(instance) {
      const journal = await call('GET', `${api}/workflow-instances/${encodeURIComponent(instance.id)}/journal`);
      return (journal.json?.items || []).some(e => e.activityType === 'Crest.Workflows.PlatformTask');
    }
    let decoyRanTask = false;
    for (const instance of forDecoy) decoyRanTask = decoyRanTask || await ranTask(instance);
    results.push({ name: 'stock-content-type-filter-is-honoured', pass: decoy.ok && !decoyRanTask, message: `decoy=${decoy.status} instancesForDecoy=${forDecoy.length} ranTask=${decoyRanTask}` });

    const createdByTask = await waitFor(async () => {
      const response = await call('GET', `${ctx.baseUrl}/api/crest/content-items?contentType=Organization&pageSize=50`);
      const hit = (response.json?.items || []).find(i => i.displayText === marker);
      return hit || null;
    });
    if (createdByTask?.contentItemId) created.items.push(createdByTask.contentItemId);
    results.push({ name: 'stock-create-content-task-created-the-content', pass: Boolean(createdByTask), message: createdByTask ? `created ${createdByTask.contentItemId}` : 'no Organization with the marker title' });

    if (forItem[0]) {
      const journal = await call('GET', `${api}/workflow-instances/${encodeURIComponent(forItem[0].id)}/journal`);
      const types = [...new Set((journal.json?.items || []).map(e => e.activityType))];
      const taskCompleted = (journal.json?.items || []).some(e => e.activityType === 'Crest.Workflows.PlatformTask' && e.eventName === 'Completed');
      results.push({ name: 'journal-shows-event-then-task', pass: types.includes('Crest.Workflows.PlatformEvent') && taskCompleted, message: types.join(',') });
    }
  } finally {
    for (const id of created.items) await call('DELETE', `${ctx.baseUrl}/api/crest/content-items/${encodeURIComponent(id)}`);
    for (const id of created.definitions) await call('DELETE', `${api}/workflow-definitions/${encodeURIComponent(id)}`);
  }

  return results;
};
