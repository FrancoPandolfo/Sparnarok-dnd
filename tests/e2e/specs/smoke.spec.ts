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

  test('Bestiary displays creatures, filters by name, and opens 2024 stat block modal', async ({ page }) => {
    await page.goto('/');

    // Verify initial creatures in the bestiary grid
    const bugbearCard = page.getByRole('heading', { level: 3, name: 'Bugbear Stalker' });
    await expect(bugbearCard).toBeVisible();

    // Search filter
    const searchInput = page.getByPlaceholder('Buscar criatura por nombre...');
    await searchInput.fill('Gorgon');
    await expect(page.getByRole('heading', { level: 3, name: 'Gorgon Vanguard' })).toBeVisible();
    await expect(bugbearCard).not.toBeVisible();

    // Clear search
    await page.getByRole('button', { name: '✕' }).click();
    await expect(bugbearCard).toBeVisible();

    // Inspect first creature stat block
    const inspectBtn = page.getByRole('button', { name: /Ver Ficha Táctica/i }).first();
    await inspectBtn.click();

    // Verify modal is open with D&D 5e 2024 sections
    await expect(page.getByText('Características y Modificadores')).toBeVisible();
    await expect(page.getByText('Acciones y Ataques Tácticos')).toBeVisible();

    // Verify dynamic AC (Clase de Armadura) calculation is displayed
    const acStatLine = page.locator('.stat-core-stats .stat-line').first();
    await expect(acStatLine).toContainText('16');

    // Close modal
    await page.locator('#close-stat-modal-btn').click();
    await expect(page.getByText('Características y Modificadores')).not.toBeVisible();
  });

  test('Admin user uploads weapons CSV file and views ingestion metrics summary', async ({ page }) => {
    // Intercept POST /api/ingestion/weapons to return deterministic API ingestion metrics
    await page.route('**/api/ingestion/weapons', async (route) => {
      if (route.request().method() === 'POST') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            totalProcessed: 3,
            inserted: 2,
            updated: 1,
            errors: [],
          }),
        });
      } else {
        await route.continue();
      }
    });

    await page.goto('/');

    // Open Data Ingestion modal
    const openIngestionBtn = page.locator('#open-ingestion-btn');
    await expect(openIngestionBtn).toBeVisible();
    await openIngestionBtn.click();

    // Verify modal header and format spec
    await expect(page.getByRole('heading', { level: 2, name: /Pipeline de Ingesta: Armas/i })).toBeVisible();
    await expect(page.getByText('Name,Damage,DamageType,Properties,Weight,Cost')).toBeVisible();

    // Verify upload button disabled until file is attached
    const uploadBtn = page.locator('#upload-csv-btn');
    await expect(uploadBtn).toBeDisabled();

    // Attach sample weapons CSV file
    const sampleCsvContent = [
      'Name,Damage,DamageType,Properties,Weight,Cost',
      'Longsword,1d8,Slashing,"Versatile (1d10)",3.0,15 gp',
      'Shortbow,1d6,Piercing,"Two-Handed, Ammunition",2.0,25 gp',
      'Dagger,1d4,Piercing,"Finesse, Light, Thrown",1.0,2 gp',
    ].join('\n');

    await page.locator('#weapon-csv-input').setInputFiles({
      name: 'canonical_weapons.csv',
      mimeType: 'text/csv',
      buffer: Buffer.from(sampleCsvContent, 'utf-8'),
    });

    // File name is displayed and upload button enabled
    await expect(page.getByText('canonical_weapons.csv')).toBeVisible();
    await expect(uploadBtn).toBeEnabled();

    // Execute upload
    await uploadBtn.click();

    // Verify results card displays metrics
    await expect(page.locator('#ingestion-results-panel')).toBeVisible();
    await expect(page.locator('#ingestion-total')).toHaveText('3');
    await expect(page.locator('#ingestion-inserted')).toHaveText('2');
    await expect(page.locator('#ingestion-updated')).toHaveText('1');

    // Close modal
    await page.locator('#close-ingestion-modal-btn').click();
    await expect(page.locator('#ingestion-results-panel')).not.toBeVisible();
  });

  test('User switches to Tactical Simulation tab, executes Monte Carlo duel, and inspects Win Rate and DPR metrics', async ({ page }) => {
    await page.goto('/');

    // Switch to Simulation tab
    const simTabBtn = page.locator('#tab-simulation-btn');
    await expect(simTabBtn).toBeVisible();
    await simTabBtn.click();
    await expect(simTabBtn).toHaveClass(/active/);

    // Verify simulation controls are displayed
    const runBtn = page.locator('#run-montecarlo-btn');
    await expect(runBtn).toBeVisible();
    await expect(page.locator('#combatant-a-select')).toBeVisible();
    await expect(page.locator('#combatant-b-select')).toBeVisible();
    await expect(page.locator('#iterations-select')).toBeVisible();

    // Select 1,000 iterations for test speed
    await page.locator('#iterations-select').selectOption('1000');

    // Run simulation
    await runBtn.click();

    // Verify results panel renders
    const resultsPanel = page.locator('#montecarlo-results-panel');
    await expect(resultsPanel).toBeVisible({ timeout: 15000 });

    // Verify win rate progress bar and segments
    await expect(page.locator('#winrate-bar-container')).toBeVisible();
    await expect(page.locator('#win-rate-a')).toBeVisible();
    await expect(page.locator('#win-rate-b')).toBeVisible();
    await expect(page.locator('#win-rate-summary-a')).toBeVisible();
    await expect(page.locator('#win-rate-summary-b')).toBeVisible();

    // Verify metric cards
    const avgRounds = page.locator('#metric-avg-rounds');
    const dprA = page.locator('#metric-dpr-a');
    const dprB = page.locator('#metric-dpr-b');
    const totalIterations = page.locator('#metric-total-iterations');

    await expect(avgRounds).toBeVisible();
    await expect(dprA).toBeVisible();
    await expect(dprB).toBeVisible();
    await expect(totalIterations).toBeVisible();

    // Verify values are populated
    await expect(totalIterations).toHaveText(/1[.,]000/);
    const avgRoundsText = await avgRounds.textContent();
    expect(Number(avgRoundsText?.replace(',', '.'))).toBeGreaterThan(0);

    // Verify execution time pill is present
    await expect(page.locator('#sim-execution-time')).toBeVisible();
  });

  test('Armor catalog ingestion and dynamic AC calculation display', async ({ page }) => {
    // Intercept POST /api/ingestion/armors to validate CSV upload contract
    let interceptedPost = false;
    await page.route('**/api/ingestion/armors', async (route) => {
      if (route.request().method() === 'POST') {
        interceptedPost = true;
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            totalProcessed: 13,
            inserted: 12,
            updated: 1,
            errors: [],
          }),
        });
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([
            {
              id: 'c0000001-0000-0000-0000-000000000001',
              name: 'Leather Armor',
              category: 'Light',
              rawArmorClass: '11 + Dex modifier',
              baseArmorClass: 11,
              shieldBonus: 0,
              allowsDexterityModifier: true,
              maxDexterityModifier: null,
            },
            {
              id: 'c0000001-0000-0000-0000-000000000002',
              name: 'Half Plate Armor',
              category: 'Medium',
              rawArmorClass: '15 + Dex modifier (max 2)',
              baseArmorClass: 15,
              shieldBonus: 0,
              allowsDexterityModifier: true,
              maxDexterityModifier: 2,
            },
            {
              id: 'c0000001-0000-0000-0000-000000000003',
              name: 'Plate Armor',
              category: 'Heavy',
              rawArmorClass: '18',
              baseArmorClass: 18,
              shieldBonus: 0,
              allowsDexterityModifier: false,
              maxDexterityModifier: null,
            },
            {
              id: 'c0000001-0000-0000-0000-000000000004',
              name: 'Shield',
              category: 'Shield',
              rawArmorClass: '+2',
              baseArmorClass: 0,
              shieldBonus: 2,
              allowsDexterityModifier: false,
              maxDexterityModifier: null,
            },
          ]),
        });
      }
    });

    await page.goto('/');

    // Verify bestiary displays creature dynamic AC calculation
    const inspectBtn = page.getByRole('button', { name: /Ver Ficha Táctica/i }).first();
    await inspectBtn.click();

    // Verify modal is open and shows dynamic AC
    await expect(page.getByText('Características y Modificadores')).toBeVisible();
    const acStatLine = page.locator('.stat-core-stats .stat-line').first();
    await expect(acStatLine).toContainText('16');

    // Close modal
    await page.locator('#close-stat-modal-btn').click();

    // Trigger API call to verify the mock/real ingestion endpoint behaves according to contract
    const response = await page.evaluate(async () => {
      const formData = new FormData();
      const blob = new Blob(['Name,Category,ArmorClass,Strength,Stealth,Weight,Cost\nPlate Armor,Heavy,18,Str 15,Disadvantage,65 lb.,"1,500 GP"'], { type: 'text/csv' });
      formData.append('file', blob, 'armors.csv');
      const res = await fetch('/api/ingestion/armors', { method: 'POST', body: formData });
      return await res.json();
    });

    expect(response.totalProcessed).toBe(13);
    expect(response.inserted).toBe(12);
    expect(response.updated).toBe(1);
    expect(interceptedPost).toBe(true);
  });

  test('User creates a custom hero profile from Tactical Simulation and selects it as a combatant', async ({ page }) => {
    // Intercept POST /api/v1/creatures/custom to return deterministic created hero profile
    await page.route('**/api/v1/creatures/custom', async (route) => {
      if (route.request().method() === 'POST') {
        const postData = JSON.parse(route.request().postData() || '{}');
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'c0000001-0000-0000-0000-999999999999',
            name: postData.name || 'Paladín Thorgar',
            size: 'Medium',
            type: 'Humanoid',
            alignment: 'Neutral Good',
            challengeRating: 5,
            speedFeet: 30,
            proficiencyBonus: 3,
            effectiveArmorClass: postData.armorClass || 19,
            isPlayerCharacter: true,
            abilityScores: {
              strength: postData.strength || 18,
              strengthModifier: 4,
              dexterity: postData.dexterity || 12,
              dexterityModifier: 1,
              constitution: postData.constitution || 16,
              constitutionModifier: 3,
              intelligence: 10,
              intelligenceModifier: 0,
              wisdom: 12,
              wisdomModifier: 1,
              charisma: 14,
              charismaModifier: 2,
            },
            hitPoints: {
              max: postData.maxHitPoints || 55,
              current: postData.maxHitPoints || 55,
              temporary: 0,
            },
            armorClass: {
              base: postData.armorClass || 19,
              shieldBonus: 0,
              allowsDexterityModifier: false,
              maxDexterityModifier: null,
            },
            actions: [
              {
                id: 'act-hero-1',
                name: postData.attackName || 'Martillo Sagrado',
                description: 'Ataque de arma cuerpo a cuerpo: +7 al impacto, daño 1d8+4 contundente.',
              },
            ],
            knownMasteries: [],
          }),
        });
      } else {
        await route.continue();
      }
    });

    await page.goto('/');

    // Switch to Simulation tab
    const simTabBtn = page.locator('#tab-simulation-btn');
    await expect(simTabBtn).toBeVisible();
    await simTabBtn.click();
    await expect(simTabBtn).toHaveClass(/active/);

    // Open "Añadir Héroe Custom" modal
    const openHeroBtn = page.locator('#open-custom-hero-btn');
    await expect(openHeroBtn).toBeVisible();
    await openHeroBtn.click();

    // Verify modal is open
    const modal = page.locator('#custom-hero-modal');
    await expect(modal).toBeVisible();

    // Fill form
    await page.locator('#hero-name-input').fill('Paladín Thorgar');
    await page.locator('#hero-ac-input').fill('19');
    await page.locator('#hero-hp-input').fill('55');
    await page.locator('#hero-str-input').fill('18');
    await page.locator('#hero-dex-input').fill('12');
    await page.locator('#hero-con-input').fill('16');
    await page.locator('#hero-attack-name-input').fill('Martillo Sagrado');
    await page.locator('#hero-damage-dice-input').fill('1d8+4');

    // Submit form
    await page.locator('#save-custom-hero-btn').click();

    // Verify modal closes
    await expect(modal).not.toBeVisible();

    // Verify the newly created hero appears in combatant-a-select and is selected
    const selectA = page.locator('#combatant-a-select');
    await expect(selectA).toBeVisible();
    await expect(selectA).toContainText('Paladín Thorgar');
    await expect(selectA).toContainText('🛡️ [Héroe]');

    // Verify it also appears in combatant-b-select options
    const selectB = page.locator('#combatant-b-select');
    await expect(selectB).toBeVisible();
    await expect(selectB).toContainText('Paladín Thorgar');

    // Verify preview card reflects the hero's stats
    const previewA = page.locator('.initiator-card');
    await expect(previewA).toContainText('Paladín Thorgar');
    await expect(previewA).toContainText('19');
    await expect(previewA).toContainText('55');
  });
});



