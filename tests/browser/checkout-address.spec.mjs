import { test, expect } from '@playwright/test';

// Match the v1 address API contract while keeping CI independent of its uptime.
// TECHVORA_ADDRESS_LIVE=true repeats these browser checks against the real API.
test.beforeEach(async ({ page }) => {
  if (process.env.TECHVORA_ADDRESS_LIVE === 'true') return;
  const province = { name: 'Tỉnh Bến Tre', code: 83 };
  const district = { name: 'Huyện Châu Thành', code: 831, province_code: 83 };
  const wards = [{ name: 'Xã Tân Thạch', code: 28804, district_code: 831 }];
  const responses = {
    '/api/v1/p/': [province],
    '/api/v1/p/83': { ...province, districts: [district] },
    '/api/v1/d/831': { ...district, wards }
  };
  await page.route('https://provinces.open-api.vn/**', async route => {
    const data = responses[new URL(route.request().url()).pathname];
    if (!data) return route.abort();
    await route.fulfill({ json: data });
  });
});

async function openCheckout(page) {
  await page.goto('/Account/Login');
  await page.locator('#Email').fill('khachhang1@shop.vn');
  await page.locator('#Password').fill('User@123');
  await Promise.all([
    page.waitForURL(url => !url.pathname.includes('/Login')),
    page.locator('button[type=submit]').click()
  ]);
  await page.goto('/Cart');
  let productId;
  const cartItem = page.locator('[name=selectedProductIds]').first();
  if (await cartItem.count()) {
    productId = await cartItem.getAttribute('value');
  } else {
    await page.goto('/Product');
    productId = await page.locator('[data-cart-product]').first().getAttribute('data-cart-product');
    const result = await page.evaluate(id => postCartAdd(id, 1), productId);
    expect(result.success).toBe(true);
  }
  await page.goto(`/Order/Checkout?selectedProductIds=${productId}`);
  await expect(page.locator('#checkoutForm')).toBeVisible();
}

async function selectAddress(page) {
  await expect(page.locator('#Province')).toBeEnabled();
  const province = page.locator('#Province option').filter({ hasText: 'Bến Tre' });
  await page.locator('#Province').selectOption(await province.getAttribute('value'));
  await expect(page.locator('#District')).toBeEnabled();
  await page.locator('#District').selectOption({ label: 'Huyện Châu Thành' });
  await expect(page.locator('#Ward')).toBeEnabled();
  await page.locator('#Ward').selectOption({ label: 'Xã Tân Thạch' });
  await expect(page.locator('#Ward')).toHaveValue('Xã Tân Thạch');
}

test('checkout can select a district and ward in Ben Tre', async ({ page }) => {
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  await openCheckout(page);
  await selectAddress(page);
  expect(errors).toEqual([]);
});

test('checkout restores the selected address after server validation', async ({ page }) => {
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  await openCheckout(page);
  await selectAddress(page);
  await page.locator('#Street').fill('');
  // Bypass client validation to exercise the server-rendered data-current values.
  // The required street stays empty, so this must not create an order.
  await Promise.all([
    page.waitForNavigation(),
    page.locator('#checkoutForm').evaluate(form => form.submit())
  ]);
  await expect(page.locator('[data-valmsg-for=Street]')).not.toBeEmpty();
  await expect(page.locator('#Province')).toHaveValue('Tỉnh Bến Tre');
  await expect(page.locator('#District')).toHaveValue('Huyện Châu Thành');
  await expect(page.locator('#Ward')).toHaveValue('Xã Tân Thạch');
  await expect(page.locator('#Ward')).toBeEnabled();
  expect(errors).toEqual([]);
});
