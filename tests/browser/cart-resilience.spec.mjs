import { test, expect } from '@playwright/test';

test.beforeEach(async ({ page }) => {
  // A fresh guest session keeps these mutations separate from demo users' carts.
  await page.goto('/Product');
  const ids = await page.locator('[data-cart-product]:not([disabled])').evaluateAll(buttons =>
    [...new Set(buttons.map(button => button.dataset.cartProduct))].slice(0, 2));
  expect(ids).toHaveLength(2);
  for (const id of ids) {
    const result = await page.evaluate(async productId => {
      const response = await postCartAdd(productId, 1);
      return response.success;
    }, id);
    expect(result).toBe(true);
  }
  await page.goto('/Cart');
  await expect(page.locator('.cart-item')).toHaveCount(2);
});

test('quantity reflects the stock limit returned by the server', async ({ page }) => {
  const input = page.locator('.cart-qty').first();
  const stock = Number(await input.getAttribute('max'));
  await input.fill(String(stock + 10));
  const [response] = await Promise.all([
    page.waitForResponse('**/Cart/Update'), input.dispatchEvent('change')
  ]);
  expect(response.ok()).toBe(true);
  await expect(input).toHaveValue(String(stock));
});

test('pending quantity changes block conflicting mutations and checkout', async ({ page }) => {
  let pending;
  await page.route('**/Cart/Update', route => { pending = route; });
  await page.locator('[data-qty-plus]').first().click();
  await expect.poll(() => Boolean(pending)).toBe(true);
  await expect(page.locator('.cart-qty').first()).toBeDisabled();
  await expect(page.locator('.cart-remove').last()).toBeDisabled();
  await page.locator('.cart-item .cart-select-cell').first().click();
  await expect(page.locator('[data-cart-item-select]').first()).not.toBeChecked();
  await expect(page.locator('#cartCheckoutSubmit')).toBeDisabled();
  await pending.continue();
  await expect(page.locator('.cart-qty').first()).toBeEnabled();
  await expect(page.locator('#cartCheckoutSubmit')).toBeEnabled();
});

test('failed quantity requests restore the last confirmed value and allow retry', async ({ page }) => {
  await page.route('**/Cart/Update', route => route.fulfill({ status: 503, body: 'Unavailable' }));
  const input = page.locator('.cart-qty').first();
  await input.fill('2');
  await input.dispatchEvent('change');
  await expect(page.locator('.toast').last()).toContainText('thử lại');
  await expect(input).toHaveValue('1');
  await expect(input).toBeEnabled();
});

test('blank quantity input does not remove a cart item', async ({ page }) => {
  const input = page.locator('.cart-qty').first();
  await input.fill('');
  await input.dispatchEvent('change');
  await expect(input).toHaveValue('1');
  await page.reload();
  await expect(page.locator('.cart-item')).toHaveCount(2);
});

test('cart API rejects malformed quantities without deleting or changing the item', async ({ page }) => {
  const productId = await page.locator('.cart-item').first().getAttribute('data-product-id');
  for (const [action, quantity] of ['Update', 'Add'].flatMap(action =>
    ['', '1.5', 'abc', '2147483648', '-1'].map(quantity => [action, quantity])).concat([['Update', null]])) {
    const result = await page.evaluate(async ({ action, productId, quantity }) => {
      const body = new URLSearchParams({ productId });
      if (quantity !== null) body.set('quantity', quantity);
      const response = await fetch('/Cart/' + action, {
        method: 'POST', headers: { 'RequestVerificationToken': antiForgeryToken() },
        body
      });
      return response.json();
    }, { action, productId, quantity });
    expect(result.success, `${action}: quantity=${JSON.stringify(quantity)}`).toBe(false);
    expect(result.items.find(item => String(item.productId) === productId)?.quantity).toBe(1);
  }
});

test('removing an offer anchor refreshes the remaining item price and selected total', async ({ page }) => {
  const rows = page.locator('.cart-item');
  const remainingId = Number(await rows.last().getAttribute('data-product-id'));
  await page.route('**/Cart/Remove', route => route.fulfill({ json: {
    success: true, message: 'Đã xoá sản phẩm khỏi giỏ hàng.', itemCount: 1,
    total: '123.456 ₫', grossTotal: '123.456 ₫', crossSellDiscount: '0 ₫', hasCrossSellDiscount: false,
    items: [{ productId: remainingId, quantity: 1, lineTotal: '123.456 ₫', lineTotalValue: 123456,
      unitPrice: '123.456 ₫', regularUnitPrice: '123.456 ₫', hasCrossSellPrice: false }], groups: []
  } }));
  await rows.first().locator('.cart-remove').click();
  await expect(rows).toHaveCount(1);
  await expect(rows.first().locator('.line-total')).toHaveText('123.456 ₫');
  await expect(rows.first().locator('[data-cart-unit-price]')).toHaveText('123.456 ₫');
  await expect(page.locator('#cart-total')).toHaveText(/123[.,]456/);
  await expect(page.locator('[data-cart-cross-sell-summary]').first()).toBeHidden();
  await expect(page.locator('[data-cart-cross-sell-summary]').last()).toBeHidden();
});

test('removing the final item shows the empty cart and survives reload', async ({ page }) => {
  await page.locator('.cart-remove').first().click();
  await expect(page.locator('.cart-item')).toHaveCount(1);
  await page.locator('.cart-remove').click();
  await expect(page.getByRole('heading', { name: 'Giỏ hàng đang trống' })).toBeVisible();
  await page.reload();
  await expect(page.locator('.cart-item')).toHaveCount(0);
});
