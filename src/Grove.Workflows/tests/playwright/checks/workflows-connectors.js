const crypto = require('crypto');
const fs = require('fs');
const http = require('http');
const path = require('path');
const { fetchAntiforgeryToken } = require('../../../../../tests/playwright/harness/antiforgery');

// Connectors, the Zapier part (plans/workflows.md, phase 3). The check hosts its own
// external service on a local port (dev.sh lets the dev and test instances reach private
// addresses; tenants elsewhere cannot) and proves:
//  - connections keep their secret server-side (the API never returns it);
//  - a sample role published (Grove's fixtures: sample.content-published, filtered to
//    SampleRole) -> Call connector (bearer) POSTs the mapped item -> stock EmailTask;
//  - transient failures are retried, the per-connection rate limit holds, OAuth2 client
//    credentials fetch one token and reuse it, a path cannot leave the base URL;
//  - a signed webhook starts a flow, a missing or wrong signature is 401;
//  - Poll connector reports Changed, then Unchanged, then Changed when the resource moves;
//  - calls are background activities (plans/workflows.md › Posting on workflows): the run
//    suspends, the call goes out after the unit commits with an Idempotency-Key, the flow
//    resumes with the response; a call scheduled in a unit that fails is never made;
//  - OAuth2 authorization code: authorize redirects to the provider with a sealed state, the
//    callback exchanges the code, the connection calls with the token it was given;
//  - an OpenAPI document imports as a connection with one palette entry per operation.
module.exports = async function run(page, ctx) {
  const results = [];
  const api = `${ctx.baseUrl}/grove-workflows/api`;
  const connectionsApi = `${ctx.baseUrl}/api/grove/workflows/connections`;
  const webhooksApi = `${ctx.baseUrl}/api/grove/workflows/webhooks`;
  const mailDir = process.env.GROVE_MAIL_DIR;
  const created = { definitions: [], connections: [], items: [] };
  const stamp = Date.now();

  // --- The external service -------------------------------------------------------
  const hits = [];
  let flakyCount = 0;
  let tokenCount = 0;
  let pollValue = 'one';
  const server = http.createServer((req, res) => {
    let body = '';
    req.on('data', chunk => { body += chunk; });
    req.on('end', () => {
      hits.push({ method: req.method, url: req.url, headers: req.headers, body });
      const reply = (status, json) => { res.writeHead(status, { 'Content-Type': 'application/json' }); res.end(JSON.stringify(json)); };
      if (req.url.startsWith('/token')) {
        tokenCount++;
        // The authorization-code exchange answers with a distinct token so the call that follows proves which flow authorized it.
        if (/grant_type=authorization_code/.test(body)) return reply(200, { access_token: `code-tok-${stamp}`, refresh_token: `refresh-${stamp}`, token_type: 'bearer', expires_in: 3600 });
        return reply(200, { access_token: `tok-${stamp}`, token_type: 'bearer', expires_in: 3600 });
      }
      if (req.url.startsWith('/flaky')) { flakyCount++; return flakyCount === 1 ? reply(503, { error: 'busy' }) : reply(200, { ok: true, attempt: flakyCount }); }
      if (req.url.startsWith('/poll')) return reply(200, { value: pollValue });
      if (req.url.startsWith('/authorize')) return reply(200, { page: 'provider login' });
      return reply(200, { received: true, id: `ext-${hits.length}` });
    });
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  const external = `http://127.0.0.1:${server.address().port}`;
  const hitsFor = prefix => hits.filter(h => h.url.startsWith(prefix));

  async function call(method, url, body, { antiforgery = true } = {}) {
    const token = antiforgery ? await fetchAntiforgeryToken(page, ctx.baseUrl) : null;
    return page.evaluate(async ({ method, url, body, token }) => {
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

  // An external caller: no cookie, no antiforgery, raw body and headers of its own.
  async function post(url, rawBody, headers) {
    return page.evaluate(async ({ url, rawBody, headers }) => {
      const response = await fetch(url, { method: 'POST', credentials: 'omit', headers, body: rawBody });
      return { status: response.status, text: await response.text() };
    }, { url, rawBody, headers });
  }

  const literal = (value, typeName = 'String') => ({ typeName, expression: { type: 'Literal', value } });
  const javascript = (value, typeName = 'String') => ({ typeName, expression: { type: 'JavaScript', value } });
  const writeLine = (id, text) => ({ type: 'Grove.Workflows.WriteLine', id, version: 1, text: literal(text) });
  const connect = (from, port, to) => ({ source: { activity: from, port }, target: { activity: to, port: 'In' } });

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

  async function publish(name, activities, connections) {
    const saved = await call('POST', `${api}/workflow-definitions`, {
      model: { name: `Playwright ${name} ${stamp}`, root: { type: 'Grove.Workflows.Flowchart', id: 'flow', version: 1, activities, connections }, variables: [], inputs: [], outputs: [], outcomes: [], customProperties: {}, options: {} },
      publish: true,
    });
    const id = saved.json?.workflowDefinition?.definitionId;
    if (id) created.definitions.push(id);
    return { id, saved };
  }

  // External calls are two-phase: the execute request ends with the run suspended, the call
  // is made after that request's unit commits, and the flow resumes in the next burst; so the
  // run settles shortly after the response.
  async function execute(definitionId, { settle = true } = {}) {
    const response = await call('POST', `${api}/workflow-definitions/${encodeURIComponent(definitionId)}/execute`, {});
    const instanceId = response.json?.workflowState?.id;
    // The engine's status is Running or Finished; the wait shows as subStatus Suspended.
    const initial = response.json?.workflowState?.subStatus;
    let status = response.json?.workflowState?.status;
    if (settle && instanceId && initial === 'Suspended') {
      const settled = await waitFor(async () => {
        const instance = await call('GET', `${api}/workflow-instances/${encodeURIComponent(instanceId)}`);
        return instance.json?.status === 'Finished' || instance.json?.subStatus === 'Faulted' ? instance.json : null;
      });
      status = settled?.status ?? status;
    }
    return { response, status, initial, instanceId };
  }

  async function ranNodes(instanceId) {
    const journal = await call('GET', `${api}/workflow-instances/${encodeURIComponent(instanceId)}/journal`);
    return new Set((journal.json?.items || []).filter(e => e.eventName === 'Completed').map(e => e.activityId));
  }

  async function connection(body) {
    const response = await call('POST', connectionsApi, { ...body, key: `pw-${body.key}-${stamp}` });
    if (response.json?.key) created.connections.push(response.json.key);
    return response;
  }

  function mailsContaining(marker) {
    if (!mailDir || !fs.existsSync(mailDir)) return [];
    return fs.readdirSync(mailDir).filter(name => name.endsWith('.eml'))
      .map(name => fs.readFileSync(path.join(mailDir, name), 'utf8')).filter(text => text.includes(marker));
  }

  try {
    // 1. Connections: the registry lists the generic connectors; the secret goes in, never out.
    const registry = await call('GET', `${ctx.baseUrl}/api/grove/workflows/registry`);
    const connectorKeys = (registry.json?.connectors || []).map(c => c.key);
    results.push({ name: 'registry-lists-generic-connectors', pass: connectorKeys.includes('http') && connectorKeys.includes('webhook'), message: connectorKeys.join(',') });

    const bearerSecret = `s3cret-${stamp}`;
    const accountant = await connection({ key: 'accountant', displayName: 'Accountant API', connectorKey: 'http', baseUrl: `${external}/api/`, authKind: 'bearer', secret: bearerSecret });
    const list = await call('GET', connectionsApi);
    results.push({
      name: 'connection-secret-is-never-returned',
      pass: accountant.ok && accountant.json?.hasSecret === true && list.ok && !accountant.text.includes(bearerSecret) && !list.text.includes(bearerSecret),
      message: `create=${accountant.status} ${accountant.text.slice(0, 160)}`,
    });

    const invalid = await call('POST', connectionsApi, { key: `pw-bad-${stamp}`, baseUrl: 'ftp://example.com', authKind: 'bearer' });
    results.push({ name: 'invalid-connection-is-refused', pass: invalid.status === 400, message: `HTTP ${invalid.status} ${invalid.text.slice(0, 160)}` });

    // 2. A sample role published -> POST it to the accountant -> e-mail.
    const marker = `WFCONN${stamp}`;
    const accountantKey = accountant.json?.key;
    const itemFlow = await publish('item to accountant', [
      // Every publish raises the sample trigger; the filter keeps this to the sample role.
      { type: 'Grove.Workflows.RegistryTrigger', id: 'published', version: 1, triggerKey: literal('sample.content-published'), payloadFilter: literal('ContentType = SampleRole'), customProperties: { canStartWorkflow: true } },
      { type: 'Grove.Workflows.CallConnector', id: 'send', version: 1, connection: literal(accountantKey), method: literal('POST'), path: literal('items'),
        body: javascript(`JSON.stringify({ id: getInput("Payload").ContentItemId, type: getInput("Payload").ContentType, name: getInput("Payload").DisplayText, marker: "${marker}" })`) },
      { type: 'Grove.Workflows.PlatformExternalTask', id: 'mail', version: 1, activityName: literal('EmailTask'), propertiesJson: literal(JSON.stringify({
        Recipients: { Expression: `accounts-${stamp}@grove.example.com` },
        Subject: { Expression: `${marker} sent {{ Workflow.Input.Payload.DisplayText }} to the accountant` },
        TextBody: { Expression: `{{ Workflow.Input.Payload.DisplayText }} was sent.` },
        HtmlBody: { Expression: `<p>{{ Workflow.Input.Payload.DisplayText }} was sent.</p>` },
      })) },
    ], [connect('published', 'Done', 'send'), connect('send', 'Done', 'mail')]);
    results.push({ name: 'item-flow-publishes', pass: Boolean(itemFlow.id), message: `HTTP ${itemFlow.saved.status} ${itemFlow.saved.text.slice(0, 160)}` });

    const person = await call('POST', `${ctx.baseUrl}/api/grove/content-items`, { contentType: 'Person', displayText: `Playwright connector person ${stamp}`, publish: true });
    if (person.json?.contentItemId) created.items.push(person.json.contentItemId);
    const roleName = `Playwright connector role ${stamp}`;
    const role = await call('POST', `${ctx.baseUrl}/api/grove/content-items`, { contentType: 'SampleRole', displayText: roleName, publish: true, content: { SampleRole: { Party: { ContentItemIds: [person.json?.contentItemId] } } } });
    const roleId = role.json?.contentItemId;
    if (roleId) created.items.push(roleId);
    results.push({ name: 'sample-role-publishes', pass: person.ok && role.ok && Boolean(roleId), message: `person=${person.status} role=${role.status} ${role.text.slice(0, 160)}` });

    const delivered = await waitFor(async () => hitsFor('/api/items').find(h => h.body.includes(marker)) || null);
    let sent = null;
    try { sent = delivered ? JSON.parse(delivered.body) : null; } catch {}
    results.push({
      name: 'published-item-is-posted-to-the-connection-with-its-auth',
      pass: Boolean(delivered) && delivered.method === 'POST' && delivered.headers.authorization === `Bearer ${bearerSecret}` && sent?.id === roleId && sent?.type === 'SampleRole' && sent?.name === roleName,
      message: delivered ? `${delivered.method} ${delivered.url} auth=${delivered.headers.authorization ? 'bearer' : 'none'} body=${delivered.body.slice(0, 160)}` : `no request (hits=${hits.length})`,
    });
    results.push({ name: 'only-the-sample-role-was-sent', pass: hitsFor('/api/items').filter(h => h.body.includes(marker)).length === 1, message: `hits=${hitsFor('/api/items').length}` });

    const mails = await waitFor(async () => { const found = mailsContaining(marker); return found.length ? found : null; }, { timeout: 10000 });
    results.push({ name: 'then-the-stock-email-task-mails-it', pass: mails?.length === 1 && mails[0].includes(`sent ${roleName} to the accountant`), message: mailDir ? `mails=${mails?.length ?? 0}` : 'GROVE_MAIL_DIR is not set (run through the host dev script)' });

    // 3. Retries, rate limit, OAuth2, path escape: flows run through the execute endpoint.
    const keyed = await connection({ key: 'flaky', baseUrl: external, authKind: 'api-key', settings: { headerName: 'X-Test-Key' }, secret: 'key-1', retryCount: 2 });
    const retryFlow = await publish('retry', [
      { type: 'Grove.Workflows.CallConnector', id: 'call', version: 1, connection: literal(keyed.json?.key), method: literal('GET'), path: literal('flaky') },
      writeLine('done', 'done'), writeLine('failed', 'failed'),
    ], [connect('call', 'Done', 'done'), connect('call', 'Failed', 'failed')]);
    const retried = retryFlow.id ? await execute(retryFlow.id) : {};
    const retryNodes = retried.instanceId ? await ranNodes(retried.instanceId) : new Set();
    const flakyHits = hitsFor('/flaky');
    results.push({
      name: 'transient-failure-is-retried-with-the-api-key',
      pass: retried.status === 'Finished' && retryNodes.has('done') && flakyHits.length === 2 && flakyHits.every(h => h.headers['x-test-key'] === 'key-1'),
      message: `status=${retried.status} nodes=${[...retryNodes].join(',')} hits=${flakyHits.length}`,
    });
    const idempotencyKeys = new Set(flakyHits.map(h => h.headers['idempotency-key']).filter(Boolean));
    results.push({
      name: 'the-call-is-made-after-the-unit-commits-with-one-idempotency-key',
      pass: retried.initial === 'Suspended' && retried.status === 'Finished' && idempotencyKeys.size === 1 && flakyHits.length === 2,
      message: `initial=${retried.initial} final=${retried.status} keys=${[...idempotencyKeys].join(',')}`,
    });

    // The call and a Fail unit in one burst (a Fork): the unit is discarded, so the recorded
    // call is dropped with it - the external service never hears of it.
    const neverFlow = await publish('never called', [
      { type: 'Grove.Workflows.Fork', id: 'fork', version: 1, branches: [
        { type: 'Grove.Workflows.CallConnector', id: 'call', version: 1, connection: literal(keyed.json?.key), method: literal('GET'), path: literal('never') },
        { type: 'Grove.Workflows.FailUnit', id: 'fail', version: 1, reason: literal('after recording the call') },
      ] },
    ], []);
    const neverRun = neverFlow.id ? await execute(neverFlow.id, { settle: false }) : {};
    await page.waitForTimeout(2500);
    results.push({
      name: 'a-call-recorded-in-a-failed-unit-is-never-made',
      pass: Boolean(neverFlow.id) && neverRun.response.status === 500 && hitsFor('/never').length === 0,
      message: `publish=${neverFlow.saved?.status} run=${neverRun.response?.status} hits=${hitsFor('/never').length}`,
    });

    const limited = await connection({ key: 'limited', baseUrl: external, authKind: 'none', rateLimitPerMinute: 1 });
    const limitFlow = await publish('rate limit', [
      { type: 'Grove.Workflows.CallConnector', id: 'first', version: 1, connection: literal(limited.json?.key), method: literal('GET'), path: literal('limited') },
      { type: 'Grove.Workflows.CallConnector', id: 'second', version: 1, connection: literal(limited.json?.key), method: literal('GET'), path: literal('limited') },
      writeLine('limitedOut', 'rate limited'),
    ], [connect('first', 'Done', 'second'), connect('second', 'Failed', 'limitedOut')]);
    const limitRun = limitFlow.id ? await execute(limitFlow.id) : {};
    const limitNodes = limitRun.instanceId ? await ranNodes(limitRun.instanceId) : new Set();
    results.push({ name: 'rate-limit-fails-the-second-call-without-sending-it', pass: limitNodes.has('limitedOut') && hitsFor('/limited').length === 1, message: `nodes=${[...limitNodes].join(',')} hits=${hitsFor('/limited').length}` });

    const oauth = await connection({ key: 'oauth', baseUrl: external, authKind: 'oauth2-client-credentials', settings: { tokenUrl: `${external}/token`, clientId: 'grove', scope: 'invoices' }, secret: 'client-secret' });
    const oauthFlow = await publish('oauth', [
      { type: 'Grove.Workflows.CallConnector', id: 'one', version: 1, connection: literal(oauth.json?.key), method: literal('GET'), path: literal('secured/1') },
      { type: 'Grove.Workflows.CallConnector', id: 'two', version: 1, connection: literal(oauth.json?.key), method: literal('GET'), path: literal('secured/2') },
    ], [connect('one', 'Done', 'two')]);
    const oauthRun = oauthFlow.id ? await execute(oauthFlow.id) : {};
    const secured = hitsFor('/secured');
    const tokenRequest = hitsFor('/token')[0];
    results.push({
      name: 'oauth2-client-credentials-fetches-one-token-and-reuses-it',
      pass: oauthRun.status === 'Finished' && tokenCount === 1 && secured.length === 2 && secured.every(h => h.headers.authorization === `Bearer tok-${stamp}`) && /client_id=grove/.test(tokenRequest?.body || '') && /grant_type=client_credentials/.test(tokenRequest?.body || ''),
      message: `status=${oauthRun.status} tokens=${tokenCount} secured=${secured.length}`,
    });

    const escapeFlow = await publish('path escape', [
      { type: 'Grove.Workflows.CallConnector', id: 'call', version: 1, connection: literal(accountantKey), method: literal('GET'), path: literal('http://127.0.0.2/elsewhere') },
      writeLine('refused', 'refused'),
    ], [connect('call', 'Failed', 'refused')]);
    const before = hits.length;
    const escapeRun = escapeFlow.id ? await execute(escapeFlow.id) : {};
    const escapeNodes = escapeRun.instanceId ? await ranNodes(escapeRun.instanceId) : new Set();
    results.push({ name: 'a-path-cannot-leave-the-base-url', pass: escapeNodes.has('refused') && hits.length === before, message: `nodes=${[...escapeNodes].join(',')} newHits=${hits.length - before}` });

    // 4. Signed webhooks.
    const signingKey = `whsec-${stamp}`;
    const hook = await connection({ key: 'hook', connectorKey: 'webhook', authKind: 'hmac', secret: signingKey });
    const hookKey = hook.json?.key;
    const hookFlow = await publish('webhook', [
      { type: 'Grove.Workflows.WebhookReceived', id: 'received', version: 1, connection: literal(hookKey), hook: literal('invoice-paid'), customProperties: { canStartWorkflow: true } },
      writeLine('log', 'paid'),
    ], [connect('received', 'Done', 'log')]);
    results.push({ name: 'webhook-flow-publishes', pass: hook.ok && Boolean(hookFlow.id), message: `connection=${hook.status} ${hook.text.slice(0, 120)}` });

    const payload = JSON.stringify({ invoice: `INV-${stamp}`, amount: 250 });
    const signature = crypto.createHmac('sha256', signingKey).update(payload).digest('hex');
    const url = `${webhooksApi}/${hookKey}/invoice-paid?source=bank`;
    const unsigned = await post(url, payload, { 'Content-Type': 'application/json' });
    const forged = await post(url, payload, { 'Content-Type': 'application/json', 'X-Grove-Signature': `sha256=${crypto.createHmac('sha256', 'wrong').update(payload).digest('hex')}` });
    const unknown = await post(`${webhooksApi}/pw-nope-${stamp}/invoice-paid`, payload, { 'Content-Type': 'application/json', 'X-Grove-Signature': signature });
    results.push({ name: 'webhook-without-valid-signature-is-refused', pass: unsigned.status === 401 && forged.status === 401 && unknown.status === 404, message: `unsigned=${unsigned.status} forged=${forged.status} unknown=${unknown.status}` });

    const signed = await post(url, payload, { 'Content-Type': 'application/json', 'X-Grove-Signature': `sha256=${signature}` });
    const hookInstances = hookFlow.id ? await waitFor(async () => {
      const response = await call('GET', `${api}/workflow-instances?definitionId=${encodeURIComponent(hookFlow.id)}`);
      return (response.json?.items || []).some(i => i.status === 'Finished') ? response.json.items : null;
    }) : null;
    let started = null;
    try { started = JSON.parse(signed.text).started; } catch {}
    results.push({ name: 'signed-webhook-starts-the-flow', pass: signed.status === 202 && started === 1 && hookInstances?.length === 1, message: `HTTP ${signed.status} ${signed.text} instances=${hookInstances?.length ?? 0}` });

    // 4b. OAuth2 authorization code, end to end against the local provider.
    const codeConnection = await connection({ key: 'oauthcode', baseUrl: external, authKind: 'oauth2-authorization-code', settings: { authorizeUrl: `${external}/authorize`, tokenUrl: `${external}/token`, clientId: 'grove-code', scope: 'ledger' }, secret: 'code-client-secret' });
    const codeKey = codeConnection.json?.key;
    // The browser's own request context (same cookies) with redirects off: the 302 to the provider is the assertion.
    const authorize = await page.context().request.get(`${connectionsApi}/${encodeURIComponent(codeKey)}/oauth/authorize`, { maxRedirects: 0 });
    const location = authorize.headers()['location'] || '';
    const params = new URLSearchParams(location.split('?')[1] || '');
    const state = params.get('state');
    results.push({
      name: 'oauth-authorize-redirects-to-the-provider-with-a-sealed-state',
      pass: authorize.status() === 302 && location.startsWith(`${external}/authorize`) && Boolean(state) && params.get('client_id') === 'grove-code' && params.get('response_type') === 'code' && params.get('scope') === 'ledger' && /oauth\/callback/.test(params.get('redirect_uri') || ''),
      message: `HTTP ${authorize.status()} location=${location.slice(0, 120)}`,
    });
    const callback = state ? await call('GET', `${connectionsApi}/oauth/callback?code=abc-${stamp}&state=${encodeURIComponent(state)}`) : { status: 0, text: 'no state' };
    const codeExchange = hitsFor('/token').find(h => /grant_type=authorization_code/.test(h.body));
    const authorizedModel = codeKey ? (await call('GET', `${connectionsApi}/${encodeURIComponent(codeKey)}`)).json : null;
    results.push({
      name: 'oauth-callback-exchanges-the-code-and-seals-the-tokens',
      pass: callback.ok && Boolean(codeExchange) && new RegExp(`code=abc-${stamp}`).test(codeExchange?.body || '') && /client_secret=code-client-secret/.test(codeExchange?.body || '') && authorizedModel?.authorized === true && !JSON.stringify(authorizedModel).includes(`code-tok-${stamp}`),
      message: `callback=${callback.status} exchange=${codeExchange ? 'yes' : 'no'} authorized=${authorizedModel?.authorized} ${callback.text.slice(0, 120)}`,
    });
    const codeFlow = await publish('oauth code call', [
      { type: 'Grove.Workflows.CallConnector', id: 'call', version: 1, connection: literal(codeKey), method: literal('GET'), path: literal('ledger/1') },
    ], []);
    const codeRun = codeFlow.id ? await execute(codeFlow.id) : {};
    const ledgerHit = hitsFor('/ledger')[0];
    results.push({ name: 'an-authorized-connection-calls-with-its-token', pass: codeRun.status === 'Finished' && ledgerHit?.headers.authorization === `Bearer code-tok-${stamp}`, message: `status=${codeRun.status} auth=${ledgerHit?.headers.authorization}` });

    // 4c. OpenAPI import: a connection and one palette entry per operation.
    const openApi = {
      openapi: '3.0.3', info: { title: `Imported API ${stamp}` },
      servers: [{ url: `${external}/v1` }],
      components: { securitySchemes: { key: { type: 'apiKey', in: 'header', name: 'X-Imported-Key' } } },
      security: [{ key: [] }],
      paths: { '/invoices': { post: { operationId: 'createInvoice', summary: 'Create an invoice' }, get: { operationId: 'listInvoices' } }, '/invoices/{id}': { get: { operationId: 'getInvoice' } } },
    };
    const imported = await call('POST', `${connectionsApi}/import-openapi`, { key: `pw-imported-${stamp}`, document: JSON.stringify(openApi), secret: 'imported-key' });
    if (imported.json?.connection?.key) created.connections.push(imported.json.connection.key);
    const importedModel = imported.json?.connection;
    const importedRegistry = await call('GET', `${ctx.baseUrl}/api/grove/workflows/registry`);
    const createOp = (importedRegistry.json?.activities || []).find(a => a.key === `connector.pw-imported-${stamp}.createInvoice`);
    results.push({
      name: 'an-openapi-document-imports-as-a-connection-with-operations',
      pass: imported.ok && importedModel?.authKind === 'api-key' && importedModel?.settings?.headerName === 'X-Imported-Key' && importedModel?.baseUrl === `${external}/v1` && importedModel?.operations?.length === 3 && createOp?.activityType === 'Grove.Workflows.CallConnector' && createOp?.inputs?.method === 'POST' && createOp?.inputs?.path === 'invoices',
      message: `HTTP ${imported.status} auth=${importedModel?.authKind} ops=${importedModel?.operations?.length} palette=${JSON.stringify(createOp?.inputs || null)} ${imported.text.slice(0, 120)}`,
    });

    // 5. Poll connector: Changed, Unchanged, Changed.
    const pollConnection = await connection({ key: 'poll', baseUrl: external, authKind: 'none' });
    const pollFlow = await publish('poll', [
      { type: 'Grove.Workflows.PollConnector', id: 'poll', version: 1, connection: literal(pollConnection.json?.key), path: literal('poll') },
      writeLine('changed', 'changed'), writeLine('unchanged', 'unchanged'),
    ], [connect('poll', 'Changed', 'changed'), connect('poll', 'Unchanged', 'unchanged')]);
    const outcomes = [];
    for (const value of ['one', 'one', 'two']) {
      pollValue = value;
      const run = pollFlow.id ? await execute(pollFlow.id) : {};
      const nodes = run.instanceId ? await ranNodes(run.instanceId) : new Set();
      outcomes.push(nodes.has('changed') ? 'Changed' : nodes.has('unchanged') ? 'Unchanged' : `?${run.status}`);
    }
    results.push({ name: 'poll-connector-detects-changes', pass: outcomes.join(',') === 'Changed,Unchanged,Changed', message: outcomes.join(',') });

    // The polling shape as a tenant builds it: the engine's own Timer trigger (scheduled in
    // this shell) runs the Poll connector with nobody calling the API.
    const pollsBefore = hitsFor('/timed').length;
    const timedFlow = await publish('timed poll', [
      { type: 'Grove.Workflows.Timer', id: 'every', version: 1, interval: literal('00:00:02', 'TimeSpan'), customProperties: { canStartWorkflow: true } },
      { type: 'Grove.Workflows.PollConnector', id: 'poll', version: 1, connection: literal(pollConnection.json?.key), path: literal('timed') },
    ], [connect('every', 'Done', 'poll')]);
    const timed = timedFlow.id ? await waitFor(async () => hitsFor('/timed').length >= pollsBefore + 2 || null, { timeout: 20000 }) : null;
    // Unpublish right away so the timer stops calling.
    if (timedFlow.id) {
      await call('DELETE', `${api}/workflow-definitions/${encodeURIComponent(timedFlow.id)}`);
      created.definitions.splice(created.definitions.indexOf(timedFlow.id), 1);
    }
    results.push({ name: 'timer-trigger-runs-the-poll-on-schedule', pass: Boolean(timed), message: `publish=${timedFlow.saved.status} polls=${hitsFor('/timed').length - pollsBefore}` });
  } finally {
    for (const id of created.definitions) await call('DELETE', `${api}/workflow-definitions/${encodeURIComponent(id)}`);
    for (const key of created.connections) await call('DELETE', `${connectionsApi}/${encodeURIComponent(key)}`);
    for (const id of created.items) await call('DELETE', `${ctx.baseUrl}/api/grove/content-items/${encodeURIComponent(id)}`);
    await new Promise(resolve => server.close(resolve));
  }

  return results;
};
