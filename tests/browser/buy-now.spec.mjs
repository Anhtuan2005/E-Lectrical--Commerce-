import { test, expect } from '@playwright/test';
import { signInDemo as login } from './auth.mjs';

test('buy now opens a direct checkout without changing the cart count', async ({ page }) => {
  await login(page);
  await page.goto('/Cart');
  const cartProductIds = await page.locator('[name=selectedProductIds]').evaluateAll(inputs => inputs.map(input => input.value));
  const cartCount = await page.locator('#cart-count').textContent();

  await page.goto('/Product');
  const productId = await page.locator('[data-cart-product]').evaluateAll((buttons, existingIds) => {
    const button = buttons.find(item => !existingIds.includes(item.dataset.cartProduct) && !item.disabled);
    return button?.dataset.cartProduct;
  }, cartProductIds);
  expect(productId).toBeTruthy();

  await page.goto(`/Product/Detail/${productId}`);
  let releaseCheckout;
  let markCheckoutStarted;
  const checkoutStarted = new Promise(resolve => { markCheckoutStarted = resolve; });
  const checkoutReleased = new Promise(resolve => { releaseCheckout = resolve; });
  await page.route('**/Order/Checkout?**', async route => {
    markCheckoutStarted();
    await checkoutReleased;
    await route.continue();
  });
  const buttonState = await page.locator('#btnBuyNow').evaluate(button => {
    button.click();
    return { disabled: button.disabled, text: button.textContent.trim() };
  });
  await checkoutStarted;
  try {
    expect(buttonState).toEqual({ disabled: false, text: 'Mua ngay' });
  } finally {
    releaseCheckout();
  }
  await page.waitForURL(url => url.pathname === '/Order/Checkout');

  await expect(page.locator('#cart-count')).toHaveText(cartCount.trim());
  await expect(page.locator('.summary-item')).toHaveCount(1);
  await expect(page.locator('.summary-item small')).toContainText('Số lượng: 1');

  await page.goBack();
  await expect(page).toHaveURL(new RegExp(`/Product/Detail/${productId}$`));
  await expect(page.locator('#btnBuyNow')).toBeEnabled();
  await expect(page.locator('#btnBuyNow')).toHaveText('Mua ngay');
});

test('voucher input and apply button remain on one row on mobile', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await login(page);
  await page.goto('/Product');
  const productId = await page.locator('[data-cart-product]:not([disabled])').first().getAttribute('data-cart-product');
  expect(productId).toBeTruthy();
  await page.goto(`/Order/Checkout?buyNowProductId=${productId}&buyNowQuantity=1`);

  await expect(page.locator('#applyVoucher')).toHaveText('Áp dụng');
  await page.evaluate(() => document.fonts.ready);
  await expect(page.locator('#voucherInput')).toBeVisible();
  await expect(page.locator('#applyVoucher')).toBeVisible();
  // Read both rectangles in one frame: pageIn moves their common ancestor,
  // so separate boundingBox calls can report a false vertical difference.
  await expect.poll(() => page.locator('.voucher-box').evaluate(box => {
    const input = box.querySelector('#voucherInput').getBoundingClientRect();
    const buttonElement = box.querySelector('#applyVoucher');
    const button = buttonElement.getBoundingClientRect();
    const text = document.createRange();
    text.selectNodeContents(buttonElement);
    return {
      aligned: Math.abs(input.y - button.y) < 2,
      sideBySide: input.right <= button.left,
      textOnOneLine: text.getClientRects().length === 1,
      withinViewport: input.left >= 0 && button.right <= window.innerWidth
    };
  }), { message: 'Voucher input and single-line button must fit side by side on mobile' }).toEqual({
    aligned: true, sideBySide: true, textOnOneLine: true, withinViewport: true
  });
  if (process.env.TECHVORA_CAPTURE_UI === 'true') {
    await page.locator('.voucher-box').screenshot({ path: 'artifacts/voucher-mobile-fixed.png' });
  }
});
