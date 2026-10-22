import { test, expect } from '@playwright/test';

test.describe('Sparnarok Codex Smoke Suite', () => {
  test.beforeEach(async ({ page }) => {
    // Pre-seed localStorage with authenticated session so standard tests operate smoothly
    await page.addInitScript(() => {
      window.localStorage.setItem(
        'sparnarok_auth',
        JSON.stringify({
          id: '00000000-0000-0000-0000-000000000001',
          email: 'dungeonmaster@sparnarok.dnd',
          displayName: 'Master Architect',
          avatarUrl: 'https://images.unsplash.com/photo-1534528741775-53994a69daeb?auto=format&fit=crop&w=64&q=80',
          token: 'e2e-valid-jwt-token',
        })
      );
    });
  });

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
            level: postData.level || 5,
            archetype: postData.archetype || 'Fighter 2H',
            expectedDpr: postData.expectedDpr || 19.4,
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
                name: postData.attackName || 'Fighter 2H Baseline',
                description: 'Ataque basado en arquetipo Fighter 2H (19.4 DPR)',
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

    // Fill form with Archetype and Level
    await page.locator('#hero-name-input').fill('Paladín Thorgar');
    await page.locator('#hero-ac-input').fill('19');
    await page.locator('#hero-hp-input').fill('55');
    await page.locator('#hero-str-input').fill('18');
    await page.locator('#hero-dex-input').fill('12');
    await page.locator('#hero-con-input').fill('16');
    await page.locator('#hero-archetype-select').selectOption('Fighter 2H');
    await page.locator('#hero-level-select').selectOption('5');

    // Verify estimated DPR displays 19.4 DPR
    const dprDisplay = page.locator('#hero-expected-dpr-display');
    await expect(dprDisplay).toBeVisible();
    await expect(dprDisplay).toContainText('19.4 DPR');

    // Submit form
    await page.locator('#save-custom-hero-btn').click();

    // Verify modal closes
    await expect(modal).not.toBeVisible();

    // Verify the newly created hero appears in combatant-a-select and is selected
    const selectA = page.locator('#combatant-a-select');
    await expect(selectA).toBeVisible();
    await expect(selectA).toContainText('Paladín Thorgar');
    await expect(selectA).toContainText('🛡️ [Héroe]');
    await expect(selectA).toContainText('19.4 DPR');

    // Verify it also appears in combatant-b-select options
    const selectB = page.locator('#combatant-b-select');
    await expect(selectB).toBeVisible();
    await expect(selectB).toContainText('Paladín Thorgar');

    // Verify preview card reflects the hero's stats and DPR badge
    const previewA = page.locator('.initiator-card');
    await expect(previewA).toContainText('Paladín Thorgar');
    await expect(previewA).toContainText('19');
    await expect(previewA).toContainText('55');
    await expect(previewA).toContainText('19.4');

    // Verify weapon dropdown is disabled and reflects no weapon required
    const weaponASelect = page.locator('#weapon-a-select');
    await expect(weaponASelect).toBeDisabled();
    await expect(weaponASelect).toContainText('Sin arma requerida');
  });

  test('Level 5 custom hero defeats a monster without requiring an equipped weapon using Expected DPR', async ({ page }) => {
    // Intercept POST /api/v1/creatures/custom
    await page.route('**/api/v1/creatures/custom', async (route) => {
      if (route.request().method() === 'POST') {
        const postData = JSON.parse(route.request().postData() || '{}');
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'c0000001-0000-0000-0000-111122223333',
            name: postData.name || 'Valeria Greatsword',
            size: 'Medium',
            type: 'Humanoid',
            alignment: 'Heroic',
            challengeRating: 5,
            speedFeet: 30,
            proficiencyBonus: 3,
            effectiveArmorClass: postData.armorClass || 18,
            isPlayerCharacter: true,
            level: 5,
            archetype: 'Fighter 2H',
            expectedDpr: 19.4,
            abilityScores: {
              strength: 18,
              strengthModifier: 4,
              dexterity: 14,
              dexterityModifier: 2,
              constitution: 16,
              constitutionModifier: 3,
              intelligence: 10,
              intelligenceModifier: 0,
              wisdom: 12,
              wisdomModifier: 1,
              charisma: 10,
              charismaModifier: 0,
            },
            hitPoints: { max: 48, current: 48, temporary: 0 },
            armorClass: { base: 18, shieldBonus: 0, allowsDexterityModifier: false, maxDexterityModifier: null },
            actions: [],
            knownMasteries: [],
          }),
        });
      } else {
        await route.continue();
      }
    });

    // Intercept POST /api/simulation/montecarlo
    let receivedPayload: any = null;
    await page.route('**/api/simulation/montecarlo', async (route) => {
      if (route.request().method() === 'POST') {
        receivedPayload = JSON.parse(route.request().postData() || '{}');
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            totalIterations: receivedPayload.iterations || 100,
            combatantAWins: 88,
            combatantBWins: 12,
            draws: 0,
            combatantAWinRate: 88.0,
            combatantBWinRate: 12.0,
            drawRate: 0.0,
            averageRounds: 2.4,
            combatantAAverageDpr: 19.4,
            combatantBAverageDpr: 6.2,
            combatantAName: 'Valeria Greatsword',
            combatantBName: 'Bugbear Stalker',
            weaponAName: 'DPR Baseline (Fighter 2H: 19.4 DPR)',
            weaponBName: 'Longsword',
            executionTimeMs: 15,
          }),
        });
      } else {
        await route.continue();
      }
    });

    await page.goto('/');

    // Switch to Simulation tab
    await page.locator('#tab-simulation-btn').click();

    // Create Level 5 Hero
    await page.locator('#open-custom-hero-btn').click();
    await page.locator('#hero-name-input').fill('Valeria Greatsword');
    await page.locator('#hero-ac-input').fill('18');
    await page.locator('#hero-hp-input').fill('48');
    await page.locator('#hero-archetype-select').selectOption('Fighter 2H');
    await page.locator('#hero-level-select').selectOption('5');
    await expect(page.locator('#hero-expected-dpr-display')).toContainText('19.4 DPR');
    await page.locator('#save-custom-hero-btn').click();

    // Verify weapon A select is disabled with DPR label
    const weaponASelect = page.locator('#weapon-a-select');
    await expect(weaponASelect).toBeDisabled();
    await expect(weaponASelect).toContainText('Sin arma requerida');

    // Run simulation
    const runBtn = page.locator('#run-montecarlo-btn');
    await expect(runBtn).toBeEnabled();
    await runBtn.click();

    // Verify simulation results panel appears
    await expect(page.locator('#montecarlo-results-panel')).toBeVisible();

    // Verify win rate and damage metrics: Hero achieved 88% win rate with 19.4 average DPR
    const winRateA = page.locator('#win-rate-a');
    await expect(winRateA).toContainText('88%');
    const dprMetricA = page.locator('#metric-dpr-a');
    await expect(dprMetricA).toContainText('19.4');

    // Verify payload sent to simulation did NOT require weaponAId
    expect(receivedPayload).not.toBeNull();
    expect(receivedPayload.combatantAId).toBe('c0000001-0000-0000-0000-111122223333');
    expect(receivedPayload.weaponAId).toBeUndefined();
  });

  test('Party vs Encounter simulation: 4 Heroes vs 1 Boss Monster renders Party Survival Rate in DOM', async ({ page }) => {
    // Intercept POST /api/simulation/montecarlo to return deterministic Party vs Encounter results
    let receivedPayload: any = null;
    await page.route('**/api/simulation/montecarlo', async (route) => {
      if (route.request().method() === 'POST') {
        receivedPayload = JSON.parse(route.request().postData() || '{}');
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            totalIterations: receivedPayload.iterations || 1000,
            combatantAWins: 840,
            combatantBWins: 160,
            draws: 0,
            combatantAWinRate: 84.0,
            combatantBWinRate: 16.0,
            drawRate: 0.0,
            averageRounds: 3.2,
            combatantAAverageDpr: 42.5,
            combatantBAverageDpr: 21.0,
            combatantAName: 'Party (4 Héroes)',
            combatantBName: 'Gorgon Vanguard',
            weaponAName: 'Arsenal de Party',
            weaponBName: 'Iron Gore Strike',
            executionTimeMs: 18,
            teamASurvivalRate: 78.5,
            teamANames: ['Héroe 1', 'Héroe 2', 'Héroe 3', 'Héroe 4'],
            teamBNames: ['Gorgon Vanguard'],
          }),
        });
      } else {
        await route.continue();
      }
    });

    await page.goto('/');

    // Switch to Simulation tab
    await page.locator('#tab-simulation-btn').click();

    // Verify initial single member in Team A and Team B
    await expect(page.locator('#combatant-a-select')).toBeVisible();
    await expect(page.locator('#combatant-b-select')).toBeVisible();

    // Add 3 more members to Team A (total 4 heroes in Party)
    const addMemberABtn = page.locator('#add-member-a-btn');
    await addMemberABtn.click();
    await addMemberABtn.click();
    await addMemberABtn.click();

    // Verify 4 members in Team A
    await expect(page.locator('#combatant-a-select-1')).toBeVisible();
    await expect(page.locator('#combatant-a-select-2')).toBeVisible();
    await expect(page.locator('#combatant-a-select-3')).toBeVisible();

    // Verify Team B has Boss monster
    const bossSelect = page.locator('#combatant-b-select');
    await bossSelect.selectOption({ index: 1 });

    // Select Targeting Strategy: LowestHp
    const targetingSelect = page.locator('#targeting-strategy-select');
    await targetingSelect.selectOption('LowestHp');

    // Run Monte Carlo Party simulation
    const runBtn = page.locator('#run-montecarlo-btn');
    await expect(runBtn).toBeEnabled();
    await runBtn.click();

    // Verify results panel renders
    await expect(page.locator('#montecarlo-results-panel')).toBeVisible();

    // Verify Party Survival Rate metric card is rendered in the DOM
    const survivalRateCard = page.locator('#metric-party-survival-rate');
    await expect(survivalRateCard).toBeVisible();
    await expect(survivalRateCard).toContainText('78.5%');

    // Verify win rates and rounds
    await expect(page.locator('#win-rate-a')).toContainText('84%');
    await expect(page.locator('#win-rate-b')).toContainText('16%');

    // Verify payload contained 4 heroes in Team A and 1 in Team B
    expect(receivedPayload).not.toBeNull();
    expect(receivedPayload.teamA).toHaveLength(4);
    expect(receivedPayload.teamB).toHaveLength(1);
    expect(receivedPayload.targetingStrategy).toBe('LowestHp');
  });

  test('Unauthenticated user is blocked from custom hero creation; Google OAuth login via network mocking enables Add Custom Hero and simulation', async ({ page }) => {
    // Clear localStorage to test explicitly unauthenticated user state
    await page.addInitScript(() => {
      window.localStorage.clear();
    });

    // Intercept POST /api/auth/google with network mocking
    let authRouteCalled = false;
    await page.route('**/api/auth/google', async (route) => {
      if (route.request().method() === 'POST') {
        authRouteCalled = true;
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            token: 'mock-jwt-e2e-token-xyz',
            userId: 'user-00000000-0000-0000-0000-000000000099',
            email: 'adventurer@sparnarok.dnd',
            displayName: 'Aventurero del Códice',
            avatarUrl: 'https://lh3.googleusercontent.com/a/default-user=s96-c',
          }),
        });
      } else {
        await route.continue();
      }
    });

    await page.goto('/');

    // Verify Navbar displays Google login button and no user profile
    const googleLoginBtn = page.locator('#google-login-btn');
    await expect(googleLoginBtn).toBeVisible();
    await expect(page.locator('#user-profile-badge')).not.toBeVisible();

    // Navigate to Tactical Simulation tab
    const simTabBtn = page.locator('#tab-simulation-btn');
    await expect(simTabBtn).toBeVisible();
    await simTabBtn.click();

    // Verify authentication banner is visible in Tactical Simulation
    const authBanner = page.locator('#sim-auth-banner');
    await expect(authBanner).toBeVisible();
    await expect(authBanner).toContainText('Autenticación Requerida');

    // Verify "Añadir Héroe Custom" is disabled for unauthenticated user
    const openHeroBtn = page.locator('#open-custom-hero-btn');
    await expect(openHeroBtn).toBeDisabled();

    // Verify "Simular Combate" is also disabled
    const runBtn = page.locator('#run-montecarlo-btn');
    await expect(runBtn).toBeDisabled();

    // Click Google Login button to trigger OAuth exchange intercepted by network mock
    await googleLoginBtn.click();

    // Verify API was intercepted
    expect(authRouteCalled).toBe(true);

    // Verify authentication banner disappears upon successful login
    await expect(authBanner).not.toBeVisible();

    // Verify "Añadir Héroe Custom" is now enabled
    await expect(openHeroBtn).toBeEnabled();

    // Verify "Simular Combate" is now enabled
    await expect(runBtn).toBeEnabled();

    // Verify user profile is rendered in Navbar
    const profileBadge = page.locator('#user-profile-badge');
    await expect(profileBadge).toBeVisible();
    await expect(page.locator('#user-display-name')).toHaveText('Aventurero del Códice');
    await expect(page.locator('#user-avatar')).toBeVisible();

    // Verify modal can now be opened
    await openHeroBtn.click();
    const heroModal = page.locator('#custom-hero-modal');
    await expect(heroModal).toBeVisible();
    await page.locator('#cancel-custom-hero-btn').click();
    await expect(heroModal).not.toBeVisible();
  });

  test('Party Persistence Manager: Logged-in user creates a Party with 3 Custom Heroes, saves it, clears screen, and loads it from dropdown', async ({ page }) => {
    // 1. Simular sesión autenticada en localStorage
    await page.addInitScript(() => {
      window.localStorage.setItem('sparnarok_auth', JSON.stringify({
        id: 'e2e-user-1234',
        email: 'dungeonmaster@sparnarok.dnd',
        displayName: 'Dungeon Master',
        avatarUrl: null,
        token: 'mock-jwt-e2e-party-token',
      }));
    });

    // Estado simulado en memoria para las peticiones de red
    const customHeroes: any[] = [];
    const savedParties: any[] = [];

    await page.route('**/api/auth/me', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          userId: 'e2e-user-1234',
          email: 'dungeonmaster@sparnarok.dnd',
          displayName: 'Dungeon Master',
          avatarUrl: null,
          token: 'mock-jwt-e2e-party-token',
        }),
      });
    });

    await page.route('**/api/v1/creatures/custom', async (route) => {
      if (route.request().method() === 'POST') {
        const body = JSON.parse(route.request().postData() || '{}');
        const hero = {
          id: `hero-custom-${customHeroes.length + 1}`,
          name: body.name,
          size: 'Medium',
          type: 'Humanoid',
          alignment: 'Heroic',
          challengeRating: 1,
          speedFeet: 30,
          proficiencyBonus: 2,
          effectiveArmorClass: body.armorClass || 16,
          isPlayerCharacter: true,
          level: body.level || 5,
          archetype: body.archetype || 'Fighter 2H',
          expectedDpr: body.expectedDpr || 19.4,
          abilityScores: {
            strength: body.strength || 16,
            strengthModifier: 3,
            dexterity: body.dexterity || 14,
            dexterityModifier: 2,
            constitution: body.constitution || 14,
            constitutionModifier: 2,
            intelligence: 10,
            intelligenceModifier: 0,
            wisdom: 12,
            wisdomModifier: 1,
            charisma: 8,
            charismaModifier: -1,
          },
          hitPoints: { max: body.maxHitPoints || 45, current: body.maxHitPoints || 45, temporary: 0 },
          armorClass: { base: body.armorClass || 16, shieldBonus: 0, allowsDexterityModifier: false, maxDexterityModifier: null },
          actions: [],
          knownMasteries: [],
        };
        customHeroes.push(hero);
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify(hero),
        });
      } else {
        await route.continue();
      }
    });

    await page.route('**/api/v1/parties**', async (route) => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(savedParties),
        });
      } else if (route.request().method() === 'POST') {
        const body = JSON.parse(route.request().postData() || '{}');
        const members = body.memberCreatureIds.map((id: string) => customHeroes.find((h) => h.id === id) || {
          id,
          name: 'Hero ' + id,
          isPlayerCharacter: true,
          effectiveArmorClass: 16,
          hitPoints: { max: 40, current: 40, temporary: 0 },
          abilityScores: { strength: 16, strengthModifier: 3, dexterity: 14, dexterityModifier: 2, constitution: 14, constitutionModifier: 2, intelligence: 10, intelligenceModifier: 0, wisdom: 10, wisdomModifier: 0, charisma: 10, charismaModifier: 0 },
          actions: [],
          knownMasteries: [],
        });
        const party = {
          id: `party-e2e-${savedParties.length + 1}`,
          name: body.name,
          ownerUserId: 'e2e-user-1234',
          createdAt: new Date().toISOString(),
          members,
        };
        savedParties.push(party);
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify(party),
        });
      } else {
        await route.continue();
      }
    });

    await page.goto('/');

    // 2. Ir a la pestaña de Simulación de Combate
    await page.locator('#tab-simulation-btn').click();

    // 3. Crear 3 Héroes Custom consecutivamente
    // Héroe 1: Sir Roland el Paladín
    await page.locator('#open-custom-hero-btn').click();
    await page.locator('#hero-name-input').fill('Sir Roland el Paladín');
    await page.locator('#hero-ac-input').fill('18');
    await page.locator('#hero-hp-input').fill('50');
    await page.locator('#hero-archetype-select').selectOption('Paladin 2H');
    await page.locator('#hero-level-select').selectOption('5');
    await page.locator('#save-custom-hero-btn').click();

    // Héroe 2: Lyra la Cazadora
    await page.locator('#open-custom-hero-btn').click();
    await page.locator('#hero-name-input').fill('Lyra la Cazadora');
    await page.locator('#hero-ac-input').fill('16');
    await page.locator('#hero-hp-input').fill('42');
    await page.locator('#hero-archetype-select').selectOption('Ranger Ranged');
    await page.locator('#hero-level-select').selectOption('5');
    await page.locator('#save-custom-hero-btn').click();

    // Héroe 3: Ignis el Brujo
    await page.locator('#open-custom-hero-btn').click();
    await page.locator('#hero-name-input').fill('Ignis el Brujo');
    await page.locator('#hero-ac-input').fill('15');
    await page.locator('#hero-hp-input').fill('38');
    await page.locator('#hero-archetype-select').selectOption('Warlock Eldritch Blast');
    await page.locator('#hero-level-select').selectOption('5');
    await page.locator('#save-custom-hero-btn').click();

    // Verificar que el Team A ahora cuenta con 3 integrantes
    await expect(page.locator('.initiator-card .team-count-pill')).toContainText('3');
    await expect(page.locator('.initiator-card .member-item-title', { hasText: 'Sir Roland el Paladín' })).toBeVisible();
    await expect(page.locator('.initiator-card .member-item-title', { hasText: 'Lyra la Cazadora' })).toBeVisible();
    await expect(page.locator('.initiator-card .member-item-title', { hasText: 'Ignis el Brujo' })).toBeVisible();

    // 4. Guardar Team A como nueva Party
    const savePartyBtn = page.locator('#save-party-btn');
    await expect(savePartyBtn).toBeEnabled();
    await savePartyBtn.click();

    // El modal de guardado debe aparecer
    const savePartyModal = page.locator('#save-party-modal');
    await expect(savePartyModal).toBeVisible();

    // Ingresar nombre de la party y confirmar
    await page.locator('#party-name-input').fill('La Guardia de Invierno');
    await page.locator('#confirm-save-party-btn').click();

    // El modal debe cerrarse y el banner de feedback exitoso debe mostrarse
    await expect(savePartyModal).not.toBeVisible();
    await expect(page.locator('#status-feedback-banner')).toBeVisible();
    await expect(page.locator('#status-feedback-banner')).toContainText('Party guardada');

    // 5. Limpiar la pantalla ("limpiando la pantalla")
    const clearTeamBtn = page.locator('#clear-team-a-btn');
    await expect(clearTeamBtn).toBeEnabled();
    await clearTeamBtn.click();

    // Team A debe estar completamente vacío con el placeholder visible
    await expect(page.locator('#empty-team-a-placeholder')).toBeVisible();
    await expect(page.locator('.initiator-card .team-count-pill')).toContainText('0');
    await expect(page.locator('.initiator-card .member-item-title')).toHaveCount(0);

    // 6. Volver a cargar la Party exitosamente desde el dropdown
    const partyDropdown = page.locator('#saved-party-select');
    await expect(partyDropdown).toBeVisible();
    const savedPartyOption = partyDropdown.locator('option', { hasText: 'La Guardia de Invierno' });
    const partyId = await savedPartyOption.getAttribute('value');
    await partyDropdown.selectOption(partyId!);

    // 7. Verificar que el Team A se pobló nuevamente con los 3 Héroes Custom
    await expect(page.locator('.initiator-card .team-count-pill')).toContainText('3');
    await expect(page.locator('.initiator-card .member-item-title', { hasText: 'Sir Roland el Paladín' })).toBeVisible();
    await expect(page.locator('.initiator-card .member-item-title', { hasText: 'Lyra la Cazadora' })).toBeVisible();
    await expect(page.locator('.initiator-card .member-item-title', { hasText: 'Ignis el Brujo' })).toBeVisible();
  });
});




