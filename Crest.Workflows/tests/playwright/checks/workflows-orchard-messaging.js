const fs = require('fs');
const path = require('path');
const { createInstance } = require('../../../../../OrchardCore.Crest/tests/playwright/harness/instance');
const { ensureTestUser } = require('../../../../../OrchardCore.Crest/tests/playwright/harness/testUsers');
const { fetchAntiforgeryToken } = require('../../../../../OrchardCore.Crest/tests/playwright/harness/antiforgery');

// Stock Users and Email activities through the adapters (plans/workflows.md, phase 1):
// a login raises the stock UserLoggedInEvent (Crest's JSON login raises it exactly as the
// stock MVC account controller does), the tenant's workflow manager turns it into an
// OrchardEvent stimulus, and a stock EmailTask with stock Liquid expressions sends a mail
// through the tenant's e-mail provider. dev.sh points that provider at a pickup
// directory and hands the suite CREST_MAIL_DIR, so the mail itself is asserted on.
// Also checks the registry lists stock activities as palette entries (and leaves out the
// ones the engine does natively).
module.exports = async function run(page, ctx) {
  const results = [];
  const api = `${ctx.baseUrl}/crest-workflows/api`;
  const mailDir = process.env.CREST_MAIL_DIR;
  const created = { definitions: [] };

  // On a freshly provisioned tenant the admin page is occasionally navigated once under a
  // running evaluate (cause not pinned down; plans/workflows.md, known flakes): settle and
  // retry once rather than fail the check on it.
  async function settled(fn) {
    try {
      return await fn();
    } catch (error) {
      if (!/Execution context was destroyed/.test(error.message)) throw error;
      await page.waitForLoadState('networkidle').catch(() => {});
      return fn();
    }
  }

  async function call(method, url, body) {
    return settled(() => callOnce(method, url, body));
  }

  async function callOnce(method, url, body) {
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

  function mailsContaining(marker) {
    if (!mailDir || !fs.existsSync(mailDir)) return [];
    const found = [];
    const walk = (dir) => {
      for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
        const full = path.join(dir, entry.name);
        if (entry.isDirectory()) walk(full);
        else if (entry.name.endsWith('.eml')) {
          const text = fs.readFileSync(full, 'utf8');
          if (text.includes(marker)) found.push(text);
        }
      }
    };
    walk(mailDir);
    return found;
  }

  try {
    // 1. The registry's stock palette entries.
    const registry = await call('GET', `${ctx.baseUrl}/api/crest/workflows/registry`);
    const activities = registry.json?.activities || [];
    const email = activities.find(a => a.key === 'orchard.EmailTask');
    const loggedIn = activities.find(a => a.key === 'orchard.UserLoggedInEvent');
    results.push({
      name: 'registry-lists-stock-task-on-the-task-adapter',
      pass: email?.activityType === 'Crest.Workflows.OrchardExternalTask' && email?.inputs?.activityName === 'EmailTask' && email?.isTrigger === false && email?.object === 'orchard',
      message: JSON.stringify(email ?? null).slice(0, 200),
    });
    results.push({
      name: 'registry-lists-stock-event-as-a-trigger',
      pass: loggedIn?.activityType === 'Crest.Workflows.OrchardEvent' && loggedIn?.inputs?.eventName === 'UserLoggedInEvent' && loggedIn?.isTrigger === true,
      message: JSON.stringify(loggedIn ?? null).slice(0, 200),
    });
    const native = ['orchard.ForkTask', 'orchard.IfElseTask', 'orchard.TimerEvent', 'orchard.HttpRequestEvent'].filter(key => activities.some(a => a.key === key));
    results.push({ name: 'registry-leaves-out-engine-native-stock-activities', pass: registry.ok && native.length === 0, message: native.length ? `listed: ${native.join(',')}` : `${activities.length} activities` });

    // 2. UserLoggedInEvent -> EmailTask.
    const stamp = Date.now();
    const marker = `WFMAIL${stamp}`;
    const recipient = `workflow-${stamp}@crest.example.com`;
    const flow = {
      type: 'Crest.Workflows.Flowchart', id: 'flow', version: 1,
      activities: [
        { type: 'Crest.Workflows.OrchardEvent', id: 'login', version: 1, eventName: literal('UserLoggedInEvent'), propertiesJson: literal('{}'), customProperties: { canStartWorkflow: true } },
        {
          type: 'Crest.Workflows.OrchardExternalTask', id: 'mail', version: 1, activityName: literal('EmailTask'),
          propertiesJson: literal(JSON.stringify({
            Recipients: { Expression: recipient },
            Subject: { Expression: `${marker} signed in: {{ Workflow.Input.UserName }}` },
            TextBody: { Expression: `Hello {{ Workflow.Input.UserName }}, ${marker}.` },
            HtmlBody: { Expression: `<p>Hello {{ Workflow.Input.UserName }}, ${marker}.</p>` },
          })),
        },
      ],
      connections: [{ source: { activity: 'login', port: 'Done' }, target: { activity: 'mail', port: 'In' } }],
    };
    const saved = await call('POST', `${api}/workflow-definitions`, { model: { name: `Playwright login mail ${stamp}`, root: flow, variables: [], inputs: [], outputs: [], outcomes: [], customProperties: {}, options: {} }, publish: true });
    const definitionId = saved.json?.workflowDefinition?.definitionId;
    if (definitionId) created.definitions.push(definitionId);
    results.push({ name: 'login-mail-definition-publishes', pass: saved.ok && Boolean(definitionId), message: `HTTP ${saved.status} ${saved.text.slice(0, 160)}` });
    if (!definitionId) return results;

    // A separate user signs in, in its own browser, through Crest's login API.
    const user = await settled(() => ensureTestUser(page, ctx.baseUrl, 'wfmail'));
    const session = await createInstance();
    let loginStatus;
    try {
      await session.page.goto(`${ctx.baseUrl}/login`, { waitUntil: 'domcontentloaded' });
      const token = await fetchAntiforgeryToken(session.page, ctx.baseUrl);
      loginStatus = await session.page.evaluate(async ({ url, body, token }) => {
        const response = await fetch(url, { method: 'POST', credentials: 'include', headers: { 'Content-Type': 'application/json', [token.headerName || 'RequestVerificationToken']: token.requestToken }, body: JSON.stringify(body) });
        return response.status;
      }, { url: `${ctx.baseUrl}/api/crest/auth/login`, body: { userName: user.username, password: user.password, rememberMe: false }, token });
    } finally {
      await session.browser.close().catch(() => {});
    }
    results.push({ name: 'test-user-signs-in', pass: loginStatus === 200, message: `HTTP ${loginStatus}` });

    const instances = await waitFor(async () => {
      const response = await call('GET', `${api}/workflow-instances?definitionId=${encodeURIComponent(definitionId)}`);
      const items = response.json?.items || [];
      return items.some(i => i.status === 'Finished') ? items : null;
    });
    results.push({ name: 'stock-login-event-starts-the-flow', pass: (instances || []).length === 1 && instances[0].status === 'Finished', message: `instances=${instances?.length ?? 0} status=${instances?.[0]?.status ?? '-'}` });

    if (instances?.[0]) {
      const journal = await call('GET', `${api}/workflow-instances/${encodeURIComponent(instances[0].id)}/journal`);
      const mailDone = (journal.json?.items || []).some(e => e.activityType === 'Crest.Workflows.OrchardExternalTask' && e.eventName === 'Completed');
      results.push({ name: 'stock-email-task-completes', pass: mailDone, message: [...new Set((journal.json?.items || []).map(e => `${e.activityId}:${e.eventName}`))].join(',') });
    }

    const mails = mailDir ? await waitFor(async () => { const found = mailsContaining(marker); return found.length ? found : null; }, { timeout: 10000 }) : null;
    const mail = mails?.[0] || '';
    results.push({
      name: 'stock-email-task-sent-the-mail-with-liquid',
      pass: mails?.length === 1 && mail.includes(recipient) && mail.includes(`signed in: ${user.username}`),
      message: mailDir ? `mails=${mails?.length ?? 0} ${mail.split('\n').filter(l => /^(To|Subject):/i.test(l)).join(' | ')}` : 'CREST_MAIL_DIR is not set (run through dev.sh)',
    });
  } finally {
    for (const id of created.definitions) await call('DELETE', `${api}/workflow-definitions/${encodeURIComponent(id)}`);
  }

  return results;
};
