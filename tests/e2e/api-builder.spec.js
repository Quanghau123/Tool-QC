const { test, expect } = require('@playwright/test');

test('xây chuỗi API từ contract local và lưu binding typed', async ({ page }) => {
  await page.goto('/');
  await page.getByRole('button', { name: 'Công cụ' }).click();
  await page.getByRole('button', { name: 'API Builder' }).click();
  await page.getByRole('button', { name: 'Tạo workspace' }).click();
  await page.locator('#apiBuilderOpenApiPath').fill('.artifacts/api-builder-contract-fixture.json');
  await page.getByRole('button', { name: 'Tải contract' }).click();
  await expect(page.locator('#apiBuilderStatus')).toContainText('Đã tải');
  await page.locator('#apiBuilderOperation').selectOption('CreateOrder');
  await page.getByRole('button', { name: 'Thêm operation vào chuỗi' }).click();
  await page.once('dialog', dialog => dialog.accept('orderId'));
  await page.getByRole('button', { name: 'Lưu thành biến' }).click();
  await page.locator('#apiBuilderOperation').selectOption('GetOrder');
  await page.getByRole('button', { name: 'Thêm operation vào chuỗi' }).click();
  await expect(page.locator('#apiBuilderSteps')).toContainText('GET');
  await page.locator('[data-api-request="1:0"]').selectOption('orderId');
  await page.getByRole('button', { name: 'Lưu workspace' }).click();
  await expect(page.locator('#apiBuilderStatus')).toContainText('Đã lưu workspace');
  await page.reload();
  await page.getByRole('button', { name: 'Công cụ' }).click();
  await page.getByRole('button', { name: 'API Builder' }).click();
  await expect(page.locator('[data-api-value="1:0"]')).toHaveValue('${orderId}');
  await page.getByRole('button', { name: 'Biên dịch và kiểm tra' }).click();
  await expect(page.locator('#draftEditor')).toBeVisible();
  await expect(page.locator('#draftStatus')).toContainText('Nhấn “1. Kiểm tra”');
});
