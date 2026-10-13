// Verifies an interactive component placed on the public site is genuinely interactive - a
// real circuit/WASM click handler, not just server-rendered markup. The dev recipe places the
// CrestCounter component (StartValue 5) in the site layout's Footer through a layer rule (widgets
// are retired, docs/blazor-display.md); the site renders every page as one display tree, so the
// home page carries it. BLAZOR_COUNTER_PATH lets a host point at another page.
module.exports = async function run(page, ctx) {
  const url = `${ctx.baseUrl}${process.env.BLAZOR_COUNTER_PATH || '/'}`;
  await page.goto(url, { waitUntil: 'networkidle' });

  const counter = page.locator('.rz-counter');
  const found = await counter.waitFor({ state: 'attached', timeout: 5000 }).then(() => true).catch(() => false);
  if (!found) {
    return [{
      name: 'blazor-counter-renders',
      pass: false,
      status: 'not-found',
      message: `.rz-counter not found at ${url} - is the dev recipe's counter layer placement present?`,
    }];
  }

  const results = [{ name: 'blazor-counter-renders', pass: true, message: 'ok' }];

  const countText = page.locator('.rz-counter p');
  const initial = await countText.textContent();
  results.push({
    name: 'blazor-counter-initial-value',
    pass: initial.trim() === 'Count: 5',
    message: `text=${initial.trim()}`,
  });

  const button = page.locator('.rz-counter button');
  await button.click();

  // Interactivity needs an attached circuit (Server) or downloaded WASM runtime
  // (WebAssembly) - a static-SSR click would be inert, so a real increment here is
  // proof the render mode wiring (AddInteractiveServerRenderMode /
  // AddInteractiveWebAssemblyRenderMode) actually works end-to-end, not just that the
  // markup happens to look right.
  await page.waitForFunction(
    () => document.querySelector('.rz-counter p')?.textContent?.trim() === 'Count: 6',
    { timeout: 5000 },
  ).then(() => true).catch(() => false);

  const afterClick = await countText.textContent();
  results.push({
    name: 'blazor-counter-increments-on-click',
    pass: afterClick.trim() === 'Count: 6',
    message: `text=${afterClick.trim()}`,
  });

  return results;
};
