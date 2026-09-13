import { expect } from '@playwright/test';

// Reuse each demo session within a worker so the suite does not exhaust the
// application's real login rate limit (5 attempts per IP in 10 minutes).
const sessions = new Map();

export async function signInDemo(page, role = 'User') {
  if (sessions.has(role)) {
    await page.context().addCookies(sessions.get(role));
    await page.goto(role === 'Admin' ? '/Admin/Dashboard' : '/');
    return;
  }
  await page.goto('/Account/Login');
  await page.locator('#Email').fill(role === 'Admin' ? 'admin@shop.vn' : 'khachhang1@shop.vn');
  await page.locator('#Password').fill(role === 'Admin' ? 'Admin@123' : 'User@123');
  const [response] = await Promise.all([
    page.waitForResponse(response => response.request().method() === 'POST' && response.url().includes('/Account/Login')),
    page.locator('button[type=submit]').click()
  ]);
  expect(response.status(), 'Demo login must succeed; use a fresh test server if it has been rate limited.').toBe(302);
  await page.waitForURL(url => !url.pathname.toLowerCase().includes('/account/login'));
  sessions.set(role, await page.context().cookies());
}
