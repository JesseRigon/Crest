const { createInstance } = require('../../../../../tests/playwright/harness/instance');
const { fetchAntiforgeryToken } = require('../../../../../tests/playwright/harness/antiforgery');

// Tenant isolation, the one property the engine may never lose (docs/workflows.md). Runs
// last among the workflow checks. Uses Grove's workflow fixtures (Grove.Workflows.Samples),
// enabled in the second tenant by the check itself.
// Provisions a second tenant ("wfiso", url prefix wfiso, the same setup recipe) through
// the stock Tenants admin form and the Setup page, then proves from both sides that
// nothing crosses: the Default admin's session is anonymous on the other tenant's API;
// definitions, the shipped flow's definition id, triggers and instances are per tenant; a
// content event in one tenant starts nothing in the other; and reloading one tenant's
// shell leaves the other's engine running. The second tenant stays (the suite's instance
// is throwaway; on a dev instance the check reuses it).
module.exports = async function run(page, ctx) {
  // The host's setup recipe (its RecipeName): a module check cannot know which host runs it.
  const setupRecipe = process.env.SETUP_RECIPE_NAME;
  if (!setupRecipe) {
    return [{ name: 'second-tenant-set-up', pass: false, message: 'SETUP_RECIPE_NAME is not set (run through the host dev.sh)' }];
  }
  const results = [];
  const base = ctx.baseUrl;
  const tenant = { name: 'wfiso', prefix: 'wfiso', url: `${ctx.baseUrl}/wfiso` };
  const admin = { username: process.env.ADMIN_USER || 'admin', password: process.env.ADMIN_PASSWORD || '' };
  const created = { defaultDefinitions: [], defaultItems: [] };

  function client(p, baseUrl) {
    return async function call(method, url, body) {
      const token = await fetchAntiforgeryToken(p, baseUrl);
      return p.evaluate(async ({ method, url, body, token }) => {
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
    };
  }
  const literal = (value, typeName = 'String') => ({ typeName, expression: { type: 'Literal', value } });
  async function waitFor(p, predicate, { timeout = 15000, interval = 500 } = {}) {
    const deadline = Date.now() + timeout;
    let last;
    while (Date.now() < deadline) {
      last = await predicate();
      if (last) return last;
      await p.waitForTimeout(interval);
    }
    return last;
  }
  const antiforgeryField = html => (html.match(/name="__RequestVerificationToken"[^>]*value="([^"]+)"/) || html.match(/value="([^"]+)"[^>]*name="__RequestVerificationToken"/) || [])[1];

  // 1. The second tenant exists and is set up.
  const catalog = await page.request.get(`${base}/api/grove/tenants`).then(r => r.json()).catch(() => null);
  let entry = catalog?.tenants?.find(t => t.name === tenant.name);
  if (!entry) {
    // The Grove admin theme serves its Blazor shell for plain admin GETs; the stock MVC
    // form (and its antiforgery field) is reached with the legacy-frame query.
    const form = await page.request.get(`${base}/Admin/Tenants/Create?legacy-frame=1`).then(r => r.text());
    const token = antiforgeryField(form);
    const createResponse = await page.request.post(`${base}/Admin/Tenants/Create?legacy-frame=1`, {
      maxRedirects: 0,
      form: { __RequestVerificationToken: token, Name: tenant.name, RequestUrlPrefix: tenant.prefix, RequestUrlHost: '', DatabaseProvider: 'Sqlite', RecipeName: setupRecipe, Category: 'Tests', Description: 'workflow isolation', TablePrefix: '', Schema: '', ConnectionString: '', action: 'createAndSetup' },
    });
    const location = createResponse.headers()['location'] || '';
    results.push({ name: 'second-tenant-created', pass: createResponse.status() === 302 && location.includes(tenant.prefix), message: `HTTP ${createResponse.status()} -> ${location.slice(0, 120)} token=${token ? 'yes' : 'missing'}${createResponse.status() === 302 ? '' : ' ' + (await createResponse.text()).replace(/<[^>]+>/g, ' ').replace(/\s+/g, ' ').slice(0, 300)}` });
    const setupUrl = location.startsWith('http') ? location : `${base}${location}`;
    const setupToken = (setupUrl.match(/[?&]token=([^&]+)/) || [])[1];
    const setupForm = await page.request.get(setupUrl).then(r => r.text());
    const setupResponse = await page.request.post(tenant.url, {
      maxRedirects: 0, timeout: 300000,
      form: { __RequestVerificationToken: antiforgeryField(setupForm) || '', SiteName: 'Workflow isolation', DatabaseProvider: 'Sqlite', RecipeName: setupRecipe, UserName: admin.username, Email: 'admin@wfiso.test', Password: admin.password, PasswordConfirmation: admin.password, Secret: decodeURIComponent(setupToken || ''), SiteTimeZone: 'UTC' },
    });
    results.push({ name: 'second-tenant-set-up', pass: setupResponse.status() === 302, message: `HTTP ${setupResponse.status()} ${setupResponse.status() === 302 ? '' : (await setupResponse.text()).replace(/<[^>]+>/g, ' ').replace(/\s+/g, ' ').slice(0, 300)}` });
  } else {
    results.push({ name: 'second-tenant-created', pass: true, message: `reused ${entry.name} (${entry.state})` });
  }
  const up = await waitFor(page, async () => (await page.request.get(`${tenant.url}/login`).catch(() => null))?.status() === 200 ? true : null, { timeout: 120000, interval: 2000 });
  results.push({ name: 'second-tenant-answers', pass: up === true, message: up ? `${tenant.url}/login is up` : 'login page not reachable' });
  if (up !== true) return results;

  // 2. The Default admin's session means nothing on the other tenant.
  const crossStatus = await page.evaluate(async url => { try { const r = await fetch(url, { credentials: 'include', redirect: 'manual' }); return r.status; } catch (e) { return `fetch failed: ${e.message}`; } }, `${tenant.url}/grove-workflows/api/workflow-definitions`);
  results.push({ name: 'default-session-is-anonymous-on-the-other-tenant', pass: crossStatus === 401, message: `HTTP ${crossStatus}` });

  const callDefault = client(page, base);
  const session = await createInstance();
  try {
    // The Blazor login shell does not boot under a url-prefixed tenant today (its
    // _framework assets are served at the site root only - a Grove finding, recorded in
    // plans/workflows.md), so the second tenant is entered through Grove's login API,
    // which is the same sign-in flow behind the shell's form.
    await session.page.goto(`${tenant.url}/Login`, { waitUntil: 'domcontentloaded' });
    const loginToken = await fetchAntiforgeryToken(session.page, tenant.url);
    const login = await session.page.evaluate(async ({ url, body, token }) => {
      const response = await fetch(url, { method: 'POST', credentials: 'include', headers: { 'Content-Type': 'application/json', [token.headerName || 'RequestVerificationToken']: token.requestToken }, body: JSON.stringify(body) });
      return { status: response.status, text: (await response.text()).slice(0, 200) };
    }, { url: `${tenant.url}/api/grove/auth/login`, body: { userName: admin.username, password: admin.password, rememberMe: false }, token: loginToken });
    const loggedIn = login.status === 200;
    results.push({ name: 'second-tenant-admin-can-log-in', pass: loggedIn, message: `HTTP ${login.status} ${login.text}` });
    if (!loggedIn) return results;
    const callTenant = client(session.page, tenant.url);
    const stamp = Date.now();
    const enabled = await callTenant('POST', `${tenant.url}/api/grove/features/Grove.Workflows.Samples/enable`);
    results.push({ name: 'second-tenant-enables-the-workflow-fixtures', pass: enabled.ok, message: `HTTP ${enabled.status}` });
    await waitFor(session.page, async () => ((await callTenant('GET', `${tenant.url}/api/grove/workflows/registry`)).json?.flows || []).some(f => f.key === 'samples.content-published') ? true : null);

    // 3. Definitions, shipped flow and triggers are per tenant.
    const defaultDefinition = await callDefault('POST', `${base}/grove-workflows/api/workflow-definitions`, {
      model: {
        name: `Isolation default flow ${stamp}`,
        root: { type: 'Grove.Workflows.Flowchart', id: 'flow', version: 1, activities: [
          { type: 'Grove.Content.ContentPublished', id: 'published', version: 1, contentTypes: ['Person'], customProperties: { canStartWorkflow: true } },
          { type: 'Grove.Workflows.WriteLine', id: 'log', version: 1, text: literal('default tenant') } ],
          connections: [{ source: { activity: 'published', port: 'Done' }, target: { activity: 'log', port: 'In' } }] },
        variables: [], inputs: [], outputs: [], outcomes: [], customProperties: {}, options: {},
      }, publish: true,
    });
    const defaultDefinitionId = defaultDefinition.json?.workflowDefinition?.definitionId;
    if (defaultDefinitionId) created.defaultDefinitions.push(defaultDefinitionId);

    const tenantList = await callTenant('GET', `${tenant.url}/grove-workflows/api/workflow-definitions`);
    const leaked = (tenantList.json?.items || []).some(d => d.definitionId === defaultDefinitionId || d.name === `Isolation default flow ${stamp}`);
    results.push({ name: 'definitions-do-not-cross-tenants', pass: tenantList.ok && Boolean(defaultDefinitionId) && !leaked, message: `tenant sees ${tenantList.json?.items?.length ?? '?'} definitions, leaked=${leaked}` });

    const defaultRegistry = await callDefault('GET', `${base}/api/grove/workflows/registry`);
    const tenantRegistry = await callTenant('GET', `${tenant.url}/api/grove/workflows/registry`);
    const flowDefault = (defaultRegistry.json?.flows || []).find(f => f.key === 'samples.content-published');
    const flowTenant = (tenantRegistry.json?.flows || []).find(f => f.key === 'samples.content-published');
    results.push({ name: 'shipped-flow-is-imported-per-tenant', pass: Boolean(flowDefault?.definitionId) && Boolean(flowTenant?.definitionId) && flowDefault.definitionId !== flowTenant.definitionId && flowTenant.published, message: `default=${flowDefault?.definitionId} tenant=${flowTenant?.definitionId}` });

    const tenantTriggers = await callTenant('GET', `${tenant.url}/api/grove/workflows/triggers`);
    const triggerLeak = (tenantTriggers.json || []).some(t => t.definitionId === defaultDefinitionId);
    results.push({ name: 'stored-triggers-are-per-tenant', pass: tenantTriggers.ok && !triggerLeak, message: `tenant triggers=${tenantTriggers.json?.length ?? '?'} leak=${triggerLeak}` });

    // 4. A content event in the other tenant starts nothing here.
    const tenantItem = await callTenant('POST', `${tenant.url}/api/grove/content-items`, { contentType: 'Person', displayText: `wfiso item ${stamp}`, publish: true });
    await page.waitForTimeout(3000);
    const defaultInstancesAfterForeignPublish = await callDefault('GET', `${base}/grove-workflows/api/workflow-instances?definitionId=${encodeURIComponent(defaultDefinitionId)}`);
    results.push({ name: 'foreign-tenant-content-event-starts-nothing-here', pass: tenantItem.ok && defaultInstancesAfterForeignPublish.json?.totalCount === 0, message: `tenantItem=${tenantItem.status} defaultInstances=${defaultInstancesAfterForeignPublish.json?.totalCount}` });

    const defaultItem = await callDefault('POST', `${base}/api/grove/content-items`, { contentType: 'Person', displayText: `default item ${stamp}`, publish: true });
    if (defaultItem.json?.contentItemId) created.defaultItems.push(defaultItem.json.contentItemId);
    const own = await waitFor(page, async () => { const r = await callDefault('GET', `${base}/grove-workflows/api/workflow-instances?definitionId=${encodeURIComponent(defaultDefinitionId)}`); return (r.json?.items?.length ?? 0) > 0 ? r.json.items : null; });
    results.push({ name: 'own-tenant-content-event-starts-the-flow', pass: own?.length === 1 && own[0].correlationId === defaultItem.json?.contentItemId, message: `instances=${own?.length ?? 0}` });
    const tenantInstances = await callTenant('GET', `${tenant.url}/grove-workflows/api/workflow-instances`);
    const instanceLeak = (tenantInstances.json?.items || []).some(i => i.definitionId === defaultDefinitionId);
    results.push({ name: 'instances-do-not-cross-tenants', pass: tenantInstances.ok && !instanceLeak, message: `tenant instances=${tenantInstances.json?.totalCount ?? '?'}` });

    // 5. Reloading one shell leaves the other engine running (hosted services per shell).
    // A flow that starts with a trigger waits for its stimulus when executed directly, so
    // the probe after a reload is a plain write-line flow run through the execute endpoint.
    const hello = await callDefault('POST', `${base}/grove-workflows/api/workflow-definitions`, { model: { name: `Isolation hello ${stamp}`, root: { type: 'Grove.Workflows.WriteLine', id: 'hello', version: 1, text: literal('still running') }, variables: [], inputs: [], outputs: [], outcomes: [], customProperties: {}, options: {} }, publish: true });
    const helloId = hello.json?.workflowDefinition?.definitionId;
    if (helloId) created.defaultDefinitions.push(helloId);
    const reload = await callDefault('POST', `${base}/api/grove/tenants/${tenant.name}/reload`);
    await waitFor(page, async () => (await page.request.get(`${tenant.url}/login`).catch(() => null))?.status() === 200 ? true : null, { timeout: 60000, interval: 2000 });
    const afterReloadDefault = await callDefault('POST', `${base}/grove-workflows/api/workflow-definitions/${encodeURIComponent(helloId)}/execute`, {});
    results.push({ name: 'reloading-the-other-tenant-leaves-this-engine-running', pass: reload.status === 204 && afterReloadDefault.ok && afterReloadDefault.json?.workflowState?.status === 'Finished', message: `reload=${reload.status} execute=${afterReloadDefault.status} status=${afterReloadDefault.json?.workflowState?.status}` });

    const reloadDefault = await callDefault('POST', `${base}/api/grove/tenants/Default/reload`);
    await waitFor(page, async () => (await page.request.get(`${base}/login`).catch(() => null))?.status() === 200 ? true : null, { timeout: 60000, interval: 2000 });
    // A published item in the other tenant raises the sample trigger there; its system flow runs there only.
    const foreign = await callTenant('POST', `${tenant.url}/api/grove/content-items`, { contentType: 'Person', displayText: `wfiso person ${stamp}`, publish: true });
    const foreignId = foreign.json?.contentItemId;
    const posted = foreign;
    const tenantRun = flowTenant?.definitionId ? await waitFor(session.page, async () => { const r = await callTenant('GET', `${tenant.url}/grove-workflows/api/workflow-instances?definitionId=${encodeURIComponent(flowTenant.definitionId)}&correlationId=${encodeURIComponent(foreignId)}`); return (r.json?.items?.length ?? 0) > 0 ? r.json.items : null; }) : null;
    results.push({ name: 'reloading-this-tenant-leaves-the-other-engine-running', pass: reloadDefault.status === 204 && posted.ok && tenantRun?.length === 1 && tenantRun[0].status === 'Finished', message: `reload=${reloadDefault.status} published=${posted.status} tenantInstances=${tenantRun?.length ?? 0}` });
    const defaultRunsForForeignItem = await callDefault('GET', `${base}/grove-workflows/api/workflow-instances?correlationId=${encodeURIComponent(foreignId)}`);
    results.push({ name: 'foreign-tenant-trigger-starts-nothing-here', pass: defaultRunsForForeignItem.ok && defaultRunsForForeignItem.json?.totalCount === 0, message: `defaultInstancesForForeignItem=${defaultRunsForForeignItem.json?.totalCount}` });
  } finally {
    await session.browser.close().catch(() => {});
    for (const id of created.defaultItems) await callDefault('DELETE', `${base}/api/grove/content-items/${encodeURIComponent(id)}`).catch(() => {});
    for (const id of created.defaultDefinitions) await callDefault('DELETE', `${base}/grove-workflows/api/workflow-definitions/${encodeURIComponent(id)}`).catch(() => {});
  }

  return results;
};
