const { createInstance } = require('../harness/instance');

// The login page greets with the TENANT's name (ISite.SiteName), not the platform's - each
// tenant has its own users and its own login page (docs/branding.md). The name is read
// anonymously from api/crest/site/branding, which must expose nothing else from ISite.
module.exports = async function run(page, ctx) {
  const results = [];
  const settings = await page.request.get(`${ctx.baseUrl}/api/crest/site`).then(r => r.json()).catch(() => null);
  const siteName = settings?.siteName;
  results.push({ name: 'site-name-is-known', pass: Boolean(siteName), message: `siteName=${siteName}` });

  const { browser, page: anonymous } = await createInstance();
  try {
    const branding = await anonymous.request.get(`${ctx.baseUrl}/api/crest/site/branding`);
    const body = await branding.json().catch(() => null);
    const keys = body ? Object.keys(body).sort() : [];
    results.push({
      name: 'anonymous-branding-read-is-public-safe',
      pass: branding.status() === 200 && body?.siteName === siteName && keys.join(',') === 'siteName',
      message: `HTTP ${branding.status()} keys=${keys.join(',')} siteName=${body?.siteName}`,
    });

    await anonymous.goto(`${ctx.baseUrl}/Login`, { waitUntil: 'networkidle' });
    const heading = anonymous.getByText(siteName || '\u0000', { exact: true }).first();
    const shown = siteName ? await heading.isVisible().catch(() => false) : false;
    const title = await anonymous.title();
    const platformName = await anonymous.getByText('Crest', { exact: true }).count();
    results.push({
      name: 'login-page-greets-with-the-site-name',
      pass: shown && title === siteName && platformName === 0,
      message: `shown=${shown} title="${title}" literal-Crest=${platformName}`,
    });
  } finally {
    await browser.close();
  }

  return results;
};
