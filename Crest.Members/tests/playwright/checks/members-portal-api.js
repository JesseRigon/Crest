// Live check of the member portal surfaces (plans/user-systems.md §B/§G): the
// [AllowAnonymous] portal pages are served to an anonymous visitor without the admin
// shell's login redirect (while a normal admin page still redirects), portal sign-in
// admits a member and refuses staff, the tenant JSON login refuses the member, and
// portal self-registration provisions a member of the organization with a linked
// Person. Setup (org, member) runs on the admin session; the surface probes run in a
// fresh anonymous browser context. Cleans up the users, person and org it creates.
module.exports = async function run(page, ctx) {
  const stamp = Date.now();
  const results = [];
  const check = (name, pass, message) => results.push({ name, pass, message });

  function apiOn(target) {
    return async function api(method, url, body) {
      const token = await target.evaluate(async () => {
        const response = await fetch('/api/crest/antiforgery/token', { credentials: 'include' });
        if (!response.ok) throw new Error(`antiforgery failed: ${response.status}`);
        return response.json();
      });
      return target.evaluate(
        async ({ method, url, body, token }) => {
          const response = await fetch(url, {
            method,
            credentials: 'include',
            redirect: 'manual',
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
    };
  }

  const admin = apiOn(page);
  const created = [];
  const userIds = [];
  const memberName = `pw-member-${stamp}`;
  const registeredName = `pw-registered-${stamp}`;
  const password = `Pw-${stamp}-Aa1!`;

  const anonymous = await page.context().browser().newContext({ baseURL: ctx.baseUrl });
  const visitor = await anonymous.newPage();

  try {
    const org = await admin('POST', '/api/crest/content-items', { contentType: 'Organization', displayText: `Playwright portal org ${stamp}`, publish: true });
    const orgId = org.json?.contentItemId;
    if (orgId) created.push(orgId);
    check('create-organization', !!orgId, `HTTP ${org.status} ${org.text}`);
    if (!orgId) return results;

    const member = await admin('POST', '/api/crest/members', { userName: memberName, email: `${memberName}@example.test`, password, organizationId: orgId });
    if (member.json?.userId) userIds.push(member.json.userId);
    check('create-member', member.ok && member.json?.bindings?.[0]?.organizationId === orgId, `HTTP ${member.status} ${member.text}`);

    // The seam: an anonymous GET of the portal login page is served (200, no
    // redirect to the login shell); a regular admin page still redirects.
    const loginPage = await visitor.goto(`${ctx.baseUrl}/Admin/members/login`, { waitUntil: 'domcontentloaded' });
    const loginPageOk = loginPage?.status() === 200 && (await visitor.locator('[data-testid="member-portal-login"]').count()) > 0;
    check('portal-login-page-is-public', loginPageOk, `HTTP ${loginPage?.status()} url=${visitor.url()}`);

    const dashboard = await visitor.request.get(`${ctx.baseUrl}/Admin/Dashboard`, { maxRedirects: 0 });
    check('admin-page-still-redirects-anonymous', dashboard.status() === 302 || dashboard.status() === 301, `HTTP ${dashboard.status()}`);

    const visitorApi = apiOn(visitor);
    const staffOnPortal = await visitorApi('POST', '/api/crest/members/portal/login', { userName: 'admin', password: process.env.ADMIN_PASSWORD || 'CrestRules1!', rememberMe: false, organizationId: null });
    check('portal-refuses-staff', staffOnPortal.status === 401 && Array.isArray(staffOnPortal.json?.errors), `HTTP ${staffOnPortal.status} ${staffOnPortal.text}`);

    const memberOnTenant = await visitorApi('POST', '/api/crest/auth/login', { userName: memberName, password, rememberMe: false });
    check('tenant-login-refuses-member', memberOnTenant.status === 401, `HTTP ${memberOnTenant.status} ${memberOnTenant.text}`);

    const wrongOrg = await visitorApi('POST', '/api/crest/members/portal/login', { userName: memberName, password, rememberMe: false, organizationId: 'not-an-org' });
    check('portal-refuses-unbound-organization', wrongOrg.status === 401, `HTTP ${wrongOrg.status} ${wrongOrg.text}`);

    const memberOnPortal = await visitorApi('POST', '/api/crest/members/portal/login', { userName: memberName, password, rememberMe: false, organizationId: null });
    check('portal-admits-member', memberOnPortal.ok && memberOnPortal.json?.member?.userName === memberName && memberOnPortal.json?.activeOrganizationId === orgId, `HTTP ${memberOnPortal.status} ${memberOnPortal.text}`);

    const me = await visitorApi('GET', '/api/crest/members/me', null);
    check('member-session-has-active-org', me.ok && me.json?.activeOrganizationId === orgId, `HTTP ${me.status} ${me.text}`);

    // The member home renders the org switcher for the signed-in member.
    await visitor.goto(`${ctx.baseUrl}/Admin/members`, { waitUntil: 'domcontentloaded' });
    const homeUser = visitor.locator('[data-testid="member-portal-user"]');
    let homeOk = false;
    try {
      await homeUser.waitFor({ timeout: 20000 });
      homeOk = (await homeUser.textContent())?.includes(memberName) === true;
    } catch {}
    check('portal-home-shows-member', homeOk, `url=${visitor.url()}`);

    await visitorApi('POST', '/api/crest/auth/logout', null);

    // Self-registration: a Person + a member of the org, linked both ways.
    const registered = await visitorApi('POST', '/api/crest/members/portal/register', {
      userName: registeredName,
      email: `${registeredName}@example.test`,
      password,
      firstName: 'Play',
      lastName: `Wright ${stamp}`,
      organizationId: orgId,
    });
    check('portal-registration', registered.ok, `HTTP ${registered.status} ${registered.text}`);

    const orgMembers = await admin('GET', `/api/crest/members?organizationId=${encodeURIComponent(orgId)}`, null);
    const registeredMember = orgMembers.json?.find?.((m) => m.userName === registeredName);
    if (registeredMember?.userId) userIds.push(registeredMember.userId);
    if (registeredMember?.personId) created.push(registeredMember.personId);
    check('registered-user-is-member-of-org', !!registeredMember && registeredMember.bindings?.some((b) => b.organizationId === orgId) && !!registeredMember.personId, `HTTP ${orgMembers.status} ${JSON.stringify(registeredMember)}`);

    if (registeredMember?.personId) {
      const person = await admin('GET', `/api/crest/content-items/${encodeURIComponent(registeredMember.personId)}`, null);
      const portalUser = person.json?.content?.Person?.PortalUser?.UserIds ?? person.json?.Person?.PortalUser?.UserIds;
      check('registered-person-links-back-to-user', person.ok && portalUser?.[0] === registeredMember.userId, `HTTP ${person.status} portalUser=${JSON.stringify(portalUser)}`);
    }

    const badOrg = await visitorApi('POST', '/api/crest/members/portal/register', { userName: `x-${stamp}`, email: `x-${stamp}@example.test`, password, firstName: 'X', lastName: 'Y', organizationId: 'nope' });
    check('registration-requires-known-org', badOrg.status === 400, `HTTP ${badOrg.status} ${badOrg.text}`);
  } finally {
    await anonymous.close().catch(() => {});
    for (const userId of userIds) {
      await admin('DELETE', `/api/crest/users/${encodeURIComponent(userId)}`, null).catch(() => {});
    }
    for (const id of created.reverse()) {
      await admin('DELETE', `/api/crest/content-items/${encodeURIComponent(id)}`, null).catch(() => {});
    }
  }

  return results;
};
