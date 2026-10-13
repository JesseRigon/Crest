const { createInstance } = require('./instance');
const { runHealthChecks } = require('./health');
const { cleanNewDir } = require('./screenshot-diff');

// One shared browser/page for the whole suite: launch once, log in once, then run every
// feature check against that same page — instead of each check paying its own
// launch+login cost. `checks` is a list of { name, fn(page, ctx) }, where fn returns one
// result object or an array of them ({ name, pass, message, ... }).
async function runSuite({ baseUrl, login, checks, outputRoot, skipHealthCheck = false }) {
  cleanNewDir(outputRoot);

  const { browser, page, consoleErrors } = await createInstance();
  const results = [];

  try {
    if (!skipHealthCheck) {
      const health = await runHealthChecks(page, baseUrl);
      for (const r of health) {
        results.push({ suite: 'health', ...r });
      }
      if (health.some(r => !r.pass)) {
        console.log('Health check failed — skipping feature checks.');
        return results;
      }
    }

    if (login) {
      await login(page, baseUrl);
    }

    for (const check of checks) {
      const started = Date.now();
      // Every check shares one page, so console errors logged by an earlier check stay
      // in the buffer and get attributed to whichever check drains next. Clear at the
      // boundary so a check's no-console-errors assertion only sees its own errors.
      consoleErrors.length = 0;
      try {
        const outcome = await check.fn(page, { baseUrl, outputRoot, consoleErrors });
        const list = Array.isArray(outcome) ? outcome : [outcome];
        for (const r of list) {
          results.push({ suite: check.name, ms: Date.now() - started, ...r });
        }
      } catch (error) {
        results.push({
          suite: check.name,
          name: check.name,
          pass: false,
          status: 'error',
          message: error.message,
          ms: Date.now() - started,
        });
      }
    }
  } finally {
    await browser.close();
  }

  return results;
}

function printSummary(results) {
  const failed = results.filter(r => r.pass === false);
  const noBase = results.filter(r => r.status === 'no-base');
  const updated = results.filter(r => r.status === 'base-updated');
  const passed = results.length - failed.length - noBase.length - updated.length;

  console.log(`\n${passed}/${results.length} passed` +
    (noBase.length ? `, ${noBase.length} no baseline yet` : '') +
    (updated.length ? `, ${updated.length} baseline(s) updated` : ''));

  for (const r of failed) {
    console.log(`FAIL ${r.suite} :: ${r.name} — ${r.message || r.status}`);
  }
  for (const r of noBase) {
    console.log(`NEW  ${r.suite} :: ${r.name} — ${r.message}`);
  }
  for (const r of updated) {
    console.log(`BASE ${r.suite} :: ${r.name} — ${r.message}`);
  }

  // Where the time went: total per check (a check may yield many results, all stamped
  // with the check's total), the slowest first. The suite is sequential, so this table
  // is the whole story of its wall-clock.
  const perCheck = new Map();
  for (const r of results) {
    if (typeof r.ms === 'number' && !perCheck.has(r.suite)) perCheck.set(r.suite, r.ms);
  }
  const total = [...perCheck.values()].reduce((a, b) => a + b, 0);
  const slowest = [...perCheck.entries()].sort((a, b) => b[1] - a[1]).slice(0, 15);
  console.log(`\nChecks: ${perCheck.size} in ${(total / 1000).toFixed(1)}s. Slowest:`);
  for (const [name, ms] of slowest) {
    console.log(`  ${(ms / 1000).toFixed(1).padStart(6)}s  ${name}`);
  }

  return failed.length === 0;
}

// A host's suite entry point in one call: BASE_URL (or the host's default), CHECK_FILTER
// (a substring; only matching checks run, the health gate still does), OUTPUT_ROOT (or the
// host's own folder, so screenshots never land in a repository the host only consumes),
// VERBOSE (one line per result), the admin login, the summary, and the exit code. A host
// passes its ordered check list and its defaults; nothing else.
async function runHostSuite({ checks, defaultBaseUrl, outputRoot, login, skipHealthCheck = false }) {
  const baseUrl = process.env.BASE_URL || defaultBaseUrl;
  if (!baseUrl) {
    throw new Error('runHostSuite needs BASE_URL or a defaultBaseUrl.');
  }

  const filter = process.env.CHECK_FILTER;
  const list = typeof checks === 'function' ? checks() : checks;
  const selected = filter ? list.filter(check => check.name.includes(filter)) : list;

  const results = await runSuite({
    baseUrl,
    login: login === undefined ? require('./auth').loginAsAdmin : login,
    checks: selected,
    outputRoot: process.env.OUTPUT_ROOT || outputRoot,
    skipHealthCheck,
  });

  if (process.env.VERBOSE) {
    for (const r of results) {
      console.log(`${r.pass === false ? 'FAIL' : 'ok  '} ${r.suite} :: ${r.name} — ${r.message || ''}`);
    }
  }

  const ok = printSummary(results);
  process.exitCode = ok ? 0 : 1;
  return ok;
}

module.exports = { runSuite, printSummary, runHostSuite };
