import { test, expect } from '@playwright/test';

test.describe('Sparnarok Codex Smoke Suite', () => {
  test('Application loads with localized Spanish copy and simulation counter responds to interaction', async ({ page }) => {
    await page.goto('/');

    // Verify initial Spanish heading and subtitle
    const heading = page.getByRole('heading', { level: 1 });
    await expect(heading).toHaveText('Sparnarok: El Códice del Arquitecto');

    // Verify simulation counter increment
    const counterBtn = page.getByRole('button', { name: /Simulaciones ejecutadas: 0/i });
    await expect(counterBtn).toBeVisible();

    await counterBtn.click();
    await expect(page.getByRole('button', { name: /Simulaciones ejecutadas: 1/i })).toBeVisible();
  });

  test('Toggling language switches UI to English seamlessly', async ({ page }) => {
    await page.goto('/');

    const langToggleBtn = page.getByRole('button', { name: /Cambiar a Inglés/i });
    await expect(langToggleBtn).toBeVisible();

    await langToggleBtn.click();

    // Verify header switched to English
    const heading = page.getByRole('heading', { level: 1 });
    await expect(heading).toHaveText("Sparnarok: The Architect's Codex");

    // Verify button text switched to English
    await expect(page.getByRole('button', { name: /Switch to Spanish/i })).toBeVisible();
  });
});
